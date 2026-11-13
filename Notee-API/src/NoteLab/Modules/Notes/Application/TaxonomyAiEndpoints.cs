using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public sealed record TaxonomyAiRequest(bool Confirm = false);

public static class TaxonomyAiEndpoints
{
    public const string RatePolicy = "taxonomy-ai";

    public static void AddTaxonomyAiRateLimit(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, IConfiguration config)
    {
        int perHour = Math.Max(1, config.GetValue<int?>("Ai:TaxonomyMaxPerHour") ?? 20);
        options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    }

    public static void MapTaxonomyAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").RequireAuthorization();
        group.MapPost("/{id:guid}/taxonomy-ai/estimate", async (Guid id, ClaimsPrincipal user, NoteDbContext db,
            AiProviderSelector selector, IConfiguration config, CancellationToken ct) =>
        {
            var note = await FindNote(db, user, id, ct);
            if (note is null) return Results.NotFound();
            if (!note.IsEditable) return Results.Conflict(new { message = "Restaure a nota da lixeira." });
            var prefs = await Preferences(db, note.OwnerId, ct);
            string provider = selector.Describe(prefs);
            int input = Math.Max(1, (note.Title.Value.Length + Math.Min(12_000, note.PlainText?.Length ?? 0)) / 4);
            int output = 400;
            return Results.Ok(new {
                provider, providerName = provider == "fake" ? "Simulação local (sem custo)" : selector.Active.Name,
                external = provider != "fake", consentGiven = prefs.AllowsExternalAi,
                externalConfigured = selector.ExternalConfigured, estimatedInputTokens = input, estimatedOutputTokens = output,
                estimatedCostUsd = provider == "fake" ? 0m : AiPricing.Cost(config, provider, input, output),
                maxPerHour = Math.Max(1, config.GetValue<int?>("Ai:TaxonomyMaxPerHour") ?? 20)
            });
        });

        group.MapPost("/{id:guid}/taxonomy-ai", async (Guid id, TaxonomyAiRequest request, ClaimsPrincipal user,
            NoteDbContext db, AiProviderSelector selector, IConfiguration config, ILoggerFactory logs, CancellationToken ct) =>
        {
            if (!request.Confirm) return Results.BadRequest(new { message = "Confirme depois de ver a estimativa." });
            var note = await FindNote(db, user, id, ct);
            if (note is null) return Results.NotFound();
            if (!note.IsEditable) return Results.Conflict(new { message = "Restaure a nota da lixeira." });
            var categories = await db.Categories.AsNoTracking().Where(c => c.OwnerId == note.OwnerId).ToListAsync(ct);
            var tags = await db.Tags.AsNoTracking().Where(t => t.OwnerId == note.OwnerId).ToListAsync(ct);
            var prefs = await Preferences(db, note.OwnerId, ct);
            string provider = selector.Describe(prefs);
            if (provider == "fake") return Results.Ok(TaxonomyAi.Fake(note, categories, tags));
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var completion = await selector.For(prefs).CompleteAsync(TaxonomyAi.SystemPrompt,
                    TaxonomyAi.UserPrompt(note, categories, tags), 600, timeout.Token);
                var preview = TaxonomyAi.Parse(completion.Text, categories, tags);
                return Results.Ok(new { preview.Category, preview.Tags, provider, external = true,
                    inputTokens = completion.InputTokens, outputTokens = completion.OutputTokens,
                    costUsd = AiPricing.Cost(config, provider, completion.InputTokens, completion.OutputTokens) });
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.Problem(statusCode: 422, title: "Resposta da IA inválida");
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                logs.CreateLogger("TaxonomyAi").LogWarning("Falha na sugestão de taxonomia: {Kind}", ex.GetType().Name);
                return Results.Problem(statusCode: 502, title: "Falha no provedor de IA");
            }
        }).RequireRateLimiting(RatePolicy);
    }

    private static Task<Note?> FindNote(NoteDbContext db, ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        string owner = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        return db.Notes.AsNoTracking().Include(n => n.Tags).SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == owner, ct);
    }

    private static async Task<UserPreferences> Preferences(NoteDbContext db, string owner, CancellationToken ct) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? UserPreferences.Default(owner);
}
