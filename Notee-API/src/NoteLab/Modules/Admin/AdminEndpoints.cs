using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public record AdminProviderResponse(string Id, string Name, bool Configured, bool Active, string Model, string DefaultModel);
public record AdminAiStateResponse(string ActiveProvider, DateTimeOffset? UpdatedAt, string? UpdatedBy, List<AdminProviderResponse> Providers);
public record SetActiveProviderRequest(string Provider);
public record SetModelRequest(string Model);
public record ProviderTestResponse(string Provider, bool Ok, long LatencyMs, string? Error, int Sources);

/// <summary>
/// Área administrativa (ADR-030). Acesso exige uma conta com a claim notee_role=admin, criada a partir de
/// Admin:Email/Admin:Password na configuração, e login pela rota própria /auth/admin/login, que emite um
/// token curto com Role=admin. A URL "escondida" do front é só conveniência — a proteção real é este papel.
/// </summary>
public static class AdminEndpoints
{
    public const string RoleClaim = "notee_role";
    public const string AdminRole = "admin";
    public const string Policy = "Admin";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/admin/login", async (LoginrRequest request, UserManager<IdentityUser> users, JwtSettings jwt, ILogger<Program> logger) =>
        {
            IdentityUser? user = await users.FindByEmailAsync(request.Email);
            bool isAdmin = user is not null
                           && await users.CheckPasswordAsync(user, request.Password)
                           && (await users.GetClaimsAsync(user)).Any(c => c.Type == RoleClaim && c.Value == AdminRole);
            if (!isAdmin)
            {
                logger.LogWarning("Tentativa de login administrativo recusada");
                return Results.Unauthorized(); // mesma resposta para senha errada ou conta não admin
            }

            DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(30);
            var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience,
                [new Claim(ClaimTypes.NameIdentifier, user!.Id), new Claim(ClaimTypes.Email, user.Email ?? ""), new Claim(ClaimTypes.Role, AdminRole)],
                expires: expires.UtcDateTime,
                signingCredentials: new SigningCredentials(jwt.SigningKey, SecurityAlgorithms.HmacSha256));
            logger.LogInformation("Login administrativo de {UserId}", user.Id);
            return Results.Ok(new { Token = new JwtSecurityTokenHandler().WriteToken(token), ExpiresAt = expires });
        }).RequireRateLimiting("auth");

        var admin = app.MapGroup("/admin").RequireAuthorization(Policy).WithTags("Admin");

        admin.MapGet("/ai", async (AiSettings settings, CancellationToken ct) => Results.Ok(await StateAsync(settings, ct)));

        admin.MapPut("/ai/active", async (SetActiveProviderRequest request, AiSettings settings, ClaimsPrincipal user, ILogger<Program> logger, CancellationToken ct) =>
        {
            AiProviderInfo? provider = AiProviders.Find(request.Provider);
            if (provider is null)
            {
                return Problem(400, "Provedor desconhecido.");
            }

            if (!settings.IsConfigured(provider))
            {
                return Problem(400, $"{provider.Name} não tem chave configurada no servidor ({provider.ConfigSection}:ApiKey).");
            }

            await settings.SetAsync(AiSettings.ActiveKey, provider.Id, Actor(user), ct);
            logger.LogInformation("IA ativa alterada para {Provider} por {Actor}", provider.Id, Actor(user));
            return Results.Ok(await StateAsync(settings, ct));
        });

        admin.MapPut("/ai/providers/{id}/model", async (string id, SetModelRequest request, AiSettings settings, ClaimsPrincipal user, ILogger<Program> logger, CancellationToken ct) =>
        {
            AiProviderInfo? provider = AiProviders.Find(id);
            string model = (request.Model ?? "").Trim();
            if (provider is null)
            {
                return Results.NotFound();
            }

            if (model.Length is 0 or > 100 || model.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '.' or '_' or ':' or '/')))
            {
                return Problem(400, "Nome de modelo inválido.");
            }

            await settings.SetAsync(AiSettings.ModelKey(provider.Id), model, Actor(user), ct);
            logger.LogInformation("Modelo de {Provider} alterado para {Model} por {Actor}", provider.Id, model, Actor(user));
            return Results.Ok(await StateAsync(settings, ct));
        });

        // Teste de conexão com pergunta fixa (nenhum dado de usuário é enviado).
        admin.MapPost("/ai/providers/{id}/test", async (string id, AiSettings settings, AiProviderSelector selector, CancellationToken ct) =>
        {
            AiProviderInfo? provider = AiProviders.Find(id);
            if (provider is null)
            {
                return Results.NotFound();
            }

            if (!settings.IsConfigured(provider))
            {
                return Problem(400, $"{provider.Name} não tem chave configurada no servidor.");
            }

            var watch = Stopwatch.StartNew();
            try
            {
                AiAnswer answer = await selector.Resolve(provider.Id).AnswerAsync("Responda apenas com a palavra: ok", "", ct);
                return Results.Ok(new ProviderTestResponse(provider.Id, true, watch.ElapsedMilliseconds, null, answer.Sources.Count));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(new ProviderTestResponse(provider.Id, false, watch.ElapsedMilliseconds, ex.Message, 0));
            }
        });
    }

    /// <summary>Cria/atualiza a conta administrativa a partir da configuração (Admin:Email e Admin:Password).</summary>
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        string? email = config["Admin:Email"];
        string? password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Admin:Email/Admin:Password não configurados — painel administrativo indisponível.");
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser? user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email };
            IdentityResult created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Não foi possível criar a conta admin: {Errors}", string.Join("; ", created.Errors.Select(e => e.Code)));
                return;
            }
        }

        if (!(await users.GetClaimsAsync(user)).Any(c => c.Type == RoleClaim && c.Value == AdminRole))
        {
            await users.AddClaimAsync(user, new Claim(RoleClaim, AdminRole));
        }
    }

    private static async Task<AdminAiStateResponse> StateAsync(AiSettings settings, CancellationToken ct)
    {
        settings.Invalidate();
        string active = settings.ActiveProvider;
        var record = await settings.GetRecordAsync(AiSettings.ActiveKey, ct);
        return new AdminAiStateResponse(active, record?.UpdatedAt, record?.UpdatedBy,
            AiProviders.All.Select(p => new AdminProviderResponse(p.Id, p.Name, settings.IsConfigured(p), p.Id == active, settings.ModelFor(p), p.DefaultModel)).ToList());
    }

    private static string Actor(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "desconhecido";

    private static IResult Problem(int status, string detail) =>
        Results.Problem(statusCode: status, title: "Erro de validação", detail: detail);
}
