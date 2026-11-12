using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public sealed record OrganizeAiRequest(string? Text, bool Confirm = false);

/// <summary>
/// "Organizar com IA" (ADR-037). Estimativa (sem envio) e execução (com confirmação explícita).
/// Só usa provedor externo se a conta consentiu (ADR-019); senão, a simulação local sem custo.
/// A nota não é alterada: a resposta é uma prévia que a pessoa aplica (ou descarta) no editor.
/// </summary>
public static class OrganizeAiEndpoints
{
    public const string RatePolicy = "organize-ai";

    private static readonly Meter Meter = new("Notee.Ai");
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("notee.ai.organize", description: "Execuções do organizar com IA");
    private static readonly Counter<long> Tokens = Meter.CreateCounter<long>("notee.ai.organize.tokens", description: "Tokens usados no organizar com IA");

    public static void AddOrganizeAiRateLimit(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, IConfiguration config)
    {
        int perHour = Math.Max(1, config.GetValue<int?>("Ai:OrganizeMaxPerHour") ?? 10);
        options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    }

    public static void MapOrganizeAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").RequireAuthorization();

        // Estimativa: provedor, se há consentimento, tokens e custo estimados. Não envia nada.
        group.MapPost("/{id:guid}/organize-ai/estimate", async (Guid id, OrganizeAiRequest body, ClaimsPrincipal user,
            NoteDbContext db, AiProviderSelector selector, IConfiguration config, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (await CheckAsync(db, owner, id, body.Text, ct) is { } problem)
            {
                return problem;
            }

            UserPreferences prefs = await PreferencesAsync(db, owner, ct);
            string provider = selector.Describe(prefs);
            bool external = provider != "fake";
            int estimatedInput = NoteAiOrganizer.EstimateTokens(body.Text!);
            int estimatedOutput = (int)(estimatedInput * 1.2);
            return Results.Ok(new
            {
                provider,
                providerName = external ? selector.Active.Name : "Simulação local (sem custo)",
                external,
                consentGiven = prefs.AllowsExternalAi,
                externalConfigured = selector.ExternalConfigured,
                estimatedInputTokens = estimatedInput,
                estimatedOutputTokens = estimatedOutput,
                estimatedCostUsd = external ? AiPricing.Cost(config, provider, estimatedInput, estimatedOutput) : 0m,
                maxPerHour = Math.Max(1, config.GetValue<int?>("Ai:OrganizeMaxPerHour") ?? 10),
            });
        });

        // Execução: exige confirm=true (a pessoa viu provedor e estimativa). Devolve prévia validada.
        group.MapPost("/{id:guid}/organize-ai", async (Guid id, OrganizeAiRequest body, ClaimsPrincipal user,
            NoteDbContext db, AiProviderSelector selector, IConfiguration config, ILoggerFactory logs, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!body.Confirm)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Confirmação necessária",
                    detail: "Confirme o envio depois de ver o provedor e a estimativa de custo.");
            }

            if (await CheckAsync(db, owner, id, body.Text, ct) is { } problem)
            {
                return problem;
            }

            UserPreferences prefs = await PreferencesAsync(db, owner, ct);
            string provider = selector.Describe(prefs);
            IAiService ai = selector.For(prefs);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));

            AiCompletion completion;
            try
            {
                completion = await ai.CompleteAsync(NoteAiOrganizer.SystemPrompt, NoteAiOrganizer.UserPrompt(body.Text!),
                    NoteAiOrganizer.MaxOutputTokens, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException
                                           or OperationCanceledException && !ct.IsCancellationRequested)
            {
                // Sem detalhes do provedor (podem ecoar o conteúdo).
                logs.CreateLogger("OrganizeAi").LogWarning("Falha no organizar com IA ({Provider}): {Kind}", provider, ex.GetType().Name);
                Runs.Add(1, new("provider", provider), new("result", "provider_error"));
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Falha no provedor de IA",
                    detail: "O provedor não respondeu. Nada foi alterado na nota.");
            }

            Tokens.Add((completion.InputTokens ?? 0) + (completion.OutputTokens ?? 0), new KeyValuePair<string, object?>("provider", provider));
            decimal? cost = provider == "fake" ? 0m : AiPricing.Cost(config, provider, completion.InputTokens, completion.OutputTokens);

            OrganizedNote result;
            try
            {
                result = NoteAiOrganizer.Parse(completion.Text);
                NoteAiOrganizer.Validate(body.Text!, result);
            }
            catch (OrganizeRejectedException rejected)
            {
                Runs.Add(1, new("provider", provider), new("result", "rejected"));
                return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Resposta da IA recusada",
                    detail: rejected.Message, extensions: new Dictionary<string, object?>
                    {
                        ["inputTokens"] = completion.InputTokens,
                        ["outputTokens"] = completion.OutputTokens,
                        ["costUsd"] = cost,
                    });
            }

            Runs.Add(1, new("provider", provider), new("result", "ok"));
            return Results.Ok(new
            {
                provider,
                external = provider != "fake",
                result.Title,
                result.Blocks,
                inputTokens = completion.InputTokens,
                outputTokens = completion.OutputTokens,
                costUsd = cost,
            });
        }).RequireRateLimiting(RatePolicy);
    }

    private static async Task<IResult?> CheckAsync(NoteDbContext db, string owner, Guid id, string? text, CancellationToken ct)
    {
        Note? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == owner, ct);
        if (note is null)
        {
            return Results.NotFound();
        }

        if (!note.IsEditable)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Regra de negócio violada",
                detail: "Notas na lixeira não podem ser organizadas.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Erro de validação", detail: "A nota está vazia.");
        }

        return text.Length > NoteAiOrganizer.MaxInputChars
            ? Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Erro de validação",
                detail: $"Texto acima de {NoteAiOrganizer.MaxInputChars} caracteres.")
            : null;
    }

    private static async Task<UserPreferences> PreferencesAsync(NoteDbContext db, string owner, CancellationToken ct) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? UserPreferences.Default(owner);

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
