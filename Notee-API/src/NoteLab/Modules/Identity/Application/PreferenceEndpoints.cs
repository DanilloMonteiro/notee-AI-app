using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public record PreferencesResponse(
    bool ResearchEnabled,
    bool ExternalAiConsent,
    int? ConsentVersion,
    DateTimeOffset? ConsentedAt,
    int CurrentConsentVersion,
    bool ExternalProviderConfigured,
    string EffectiveProvider,
    long Version);

/// <summary>Campos nulos não são alterados. Conceder consentimento exige ConsentVersion vigente.</summary>
public record UpdatePreferencesRequest(bool? ResearchEnabled, bool? ExternalAiConsent, int? ConsentVersion, long ExpectedVersion);

public record MeResponse(string Id, string? Email);

public sealed class PreferencesService
{
    private readonly NoteDbContext _db;
    private readonly AiProviderSelector _selector;

    public PreferencesService(NoteDbContext db, AiProviderSelector selector)
    {
        _db = db;
        _selector = selector;
    }

    /// <summary>Leitura sem efeito colateral: sem registro, devolve o padrão.</summary>
    public async Task<UserPreferences> GetAsync(string userId, CancellationToken ct) =>
        await _db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct)
        ?? UserPreferences.Default(userId);

    public PreferencesResponse ToResponse(UserPreferences p) => new(
        p.ResearchEnabled,
        p.ExternalAiConsent,
        p.ConsentVersion,
        p.ConsentedAt,
        UserPreferences.CurrentConsentVersion,
        _selector.ExternalConfigured,
        _selector.Describe(p),
        p.Version);

    public async Task<MapResult<PreferencesResponse>> UpdateAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct)
    {
        UserPreferences? prefs = await _db.UserPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        bool isNew = prefs is null;
        prefs ??= UserPreferences.Default(userId);

        if (prefs.Version != request.ExpectedVersion)
        {
            return MapResult<PreferencesResponse>.Conflict("As preferências foram alteradas em outra aba. Recarregue a página.");
        }

        try
        {
            if (request.ResearchEnabled.HasValue)
            {
                prefs.SetResearchEnabled(request.ResearchEnabled.Value);
            }

            if (request.ExternalAiConsent.HasValue)
            {
                prefs.SetExternalAiConsent(request.ExternalAiConsent.Value, request.ConsentVersion);
            }
        }
        catch (DomainException ex)
        {
            return MapResult<PreferencesResponse>.Invalid(ex.Message);
        }

        if (isNew)
        {
            _db.UserPreferences.Add(prefs);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return MapResult<PreferencesResponse>.Conflict("As preferências foram alteradas em outra requisição.");
        }

        return MapResult<PreferencesResponse>.Ok(ToResponse(prefs));
    }
}

public static class PreferenceEndpoints
{
    public static void MapPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me").RequireAuthorization().WithTags("Me");

        group.MapGet("/", async (ClaimsPrincipal user, UserManager<IdentityUser> users) =>
        {
            string id = BoardEndpoints.OwnerId(user);
            IdentityUser? identity = await users.FindByIdAsync(id);
            return identity is null ? Results.Unauthorized() : Results.Ok(new MeResponse(identity.Id, identity.Email));
        });

        group.MapGet("/preferences", async (ClaimsPrincipal user, PreferencesService service, CancellationToken ct) =>
            Results.Ok(service.ToResponse(await service.GetAsync(BoardEndpoints.OwnerId(user), ct))));

        group.MapPatch("/preferences", async (UpdatePreferencesRequest request, ClaimsPrincipal user, PreferencesService service, CancellationToken ct) =>
        {
            MapResult<PreferencesResponse> result = await service.UpdateAsync(BoardEndpoints.OwnerId(user), request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : BoardEndpoints.ToProblem(result);
        });
    }
}
