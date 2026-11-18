using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public sealed record ComposeAiRequest(string? Mode, string? Title, string? Instructions, string? BaseLink, bool Confirm = false);

/// <summary>
/// Criação de nota assistida por IA (ADR-044). Estimativa (sem envio) e geração (com confirmação explícita).
/// Nada é gravado: a resposta é uma prévia com texto, gráficos, taxonomia e fontes conferidas para aceite no cliente.
/// </summary>
public static class NoteComposeAiEndpoints
{
    public const string RatePolicy = "compose-ai";
    private const int SourceLookupParallelism = 3;
    private static readonly TimeSpan SourceLookupTimeout = TimeSpan.FromSeconds(15);

    private static readonly Meter Meter = new("Notee.Ai.Compose");
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("notee.ai.compose", description: "Execuções da criação de nota com IA");

    private sealed record ComposeInput(ComposeMode Mode, string Title, string Instructions, NoteLink? Link, string? LinkUrl);

    private sealed record BaseNote(string Title, ComposeBaseText Text, bool IsOwn, string? Url, string? AuthorName, int Year);

    public static void AddComposeAiRateLimit(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, IConfiguration config)
    {
        int perHour = MaxPerHour(config);
        options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    }

    public static void MapComposeAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").RequireAuthorization().WithTags("Notes");

        group.MapPost("/compose-ai/estimate", async (ComposeAiRequest body, ClaimsPrincipal user, NoteDbContext db,
            AiProviderSelector selector, IConfiguration config, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (Validate(body, out ComposeInput? input) is { } invalid) return invalid;
            BaseNote? baseNote = null;
            if (input!.Link is not null && (baseNote = await LoadBaseAsync(db, owner, input, ct)) is null) return BaseNotFound();

            UserPreferences prefs = await PreferencesAsync(db, owner, ct);
            string provider = selector.Describe(prefs);
            bool external = provider != "fake";
            string prompt = await PromptAsync(db, owner, input, baseNote, ct);
            int estimatedInput = NoteComposer.EstimateTokens(prompt);
            int estimatedOutput = Math.Min(NoteComposer.MaxOutputTokens, 1_500 + estimatedInput);
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
                maxPerHour = MaxPerHour(config),
                baseNote = baseNote is null ? null : new { baseNote.Title, baseNote.IsOwn },
            });
        });

        group.MapPost("/compose-ai", async (ComposeAiRequest body, ClaimsPrincipal user, NoteDbContext db,
            AiProviderSelector selector, SafeHttpClient http, IConfiguration config, ILoggerFactory logs, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!body.Confirm)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Confirmação necessária",
                    detail: "Confirme o envio depois de ver o provedor e a estimativa de custo.");
            }

            if (Validate(body, out ComposeInput? input) is { } invalid) return invalid;
            BaseNote? baseNote = null;
            if (input!.Link is not null && (baseNote = await LoadBaseAsync(db, owner, input, ct)) is null) return BaseNotFound();

            var categories = await db.Categories.AsNoTracking().Where(c => c.OwnerId == owner).OrderBy(c => c.Name).ToListAsync(ct);
            var tags = await db.Tags.AsNoTracking().Where(t => t.OwnerId == owner).OrderBy(t => t.Name).ToListAsync(ct);
            UserPreferences prefs = await PreferencesAsync(db, owner, ct);
            string provider = selector.Describe(prefs);
            string prompt = NoteComposer.UserPrompt(input.Mode, input.Title, input.Instructions, baseNote?.Title, baseNote?.Text, categories, tags);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            AiCompletion completion;
            try
            {
                completion = await selector.For(prefs).CompleteAsync(NoteComposer.SystemPrompt, prompt, NoteComposer.MaxOutputTokens, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException
                                           or OperationCanceledException && !ct.IsCancellationRequested)
            {
                // Sem detalhes do provedor (podem ecoar o conteúdo).
                logs.CreateLogger("ComposeAi").LogWarning("Falha na criação com IA ({Provider}): {Kind}", provider, ex.GetType().Name);
                Runs.Add(1, new("provider", provider), new("result", "provider_error"));
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Falha no provedor de IA",
                    detail: "O provedor não respondeu. Nenhuma nota foi criada.");
            }

            decimal? cost = provider == "fake" ? 0m : AiPricing.Cost(config, provider, completion.InputTokens, completion.OutputTokens);
            ComposeDraft draft;
            try
            {
                draft = NoteComposer.Parse(completion.Text, baseNote?.Text.Charts.Count ?? 0, categories, tags);
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

            // Fontes da nota e dos gráficos novos: só fica o que a resolução de DOI/ISBN/URL encontrar.
            var identifiers = draft.Sources.Select(s => s.Identifier)
                .Concat(draft.NewCharts.Select(c => c.SourceIdentifier))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            IReadOnlyDictionary<string, ResolvedSource> resolved = await ResolveAllAsync(identifiers, http, config, ct);
            ComposeResult result = NoteComposer.Assemble(draft, baseNote?.Text.Charts ?? [], resolved);

            Runs.Add(1, new("provider", provider), new("result", "ok"));
            return Results.Ok(new
            {
                provider,
                external = provider != "fake",
                title = result.Title ?? input.Title,
                blocks = result.Blocks.Select(b => new { b.Type, b.Text, b.Level, b.Ref }),
                charts = result.Charts.Select(c => new { chart = c.Chart, c.Origin, c.SourceIndex }),
                category = result.Taxonomy.Category,
                tags = result.Taxonomy.Tags,
                // supports: trechos exatos do texto que a fonte apoia (viram citações ancoradas no cliente).
                sources = result.Sources.Select(s => new { s.Identifier, s.Reason, s.Resolved, supports = s.Supports ?? [] }),
                // Origens sem DOI/ISBN/URL conferido: exibidas como "Origem informada pela IA — não conferida", sem link.
                origins = (result.Origins ?? []).Select(o => new { o.Label, o.Supports, unresolvedIdentifier = o.FromUnresolvedIdentifier }),
                attribution = baseNote is { IsOwn: false } ? new
                {
                    kind = "website",
                    title = baseNote.Title,
                    authors = baseNote.AuthorName is null ? Array.Empty<string>() : [baseNote.AuthorName],
                    year = baseNote.Year,
                    url = baseNote.Url,
                } : null,
                discardedSources = result.DiscardedSources,
                discardedCharts = result.DiscardedCharts,
                inputTokens = completion.InputTokens,
                outputTokens = completion.OutputTokens,
                costUsd = cost,
            });
        }).RequireRateLimiting(RatePolicy);
    }

    private static IResult? Validate(ComposeAiRequest body, out ComposeInput? input)
    {
        input = null;
        string title = (body.Title ?? "").Trim();
        if (title.Length is < 1 or > 100)
        {
            return ValidationProblem("O título deve ter entre 1 e 100 caracteres.");
        }

        string instructions = (body.Instructions ?? "").Trim();
        if (instructions.Length > NoteComposer.MaxInstructionsChars)
        {
            return ValidationProblem($"A descrição deve ter no máximo {NoteComposer.MaxInstructionsChars} caracteres.");
        }

        ComposeMode mode;
        switch ((body.Mode ?? "").Trim().ToLowerInvariant())
        {
            case "generate":
                mode = ComposeMode.Generate;
                if (instructions.Length == 0) return ValidationProblem("Descreva o que você quer na nota.");
                input = new ComposeInput(mode, title, instructions, null, null);
                return null;
            case "rewrite":
                mode = ComposeMode.Rewrite;
                NoteLink? link = NoteLinkParser.Parse(body.BaseLink);
                if (link is null) return ValidationProblem("Link de nota não reconhecido. Use /note/…, /p/…/… ou /s/….");
                input = new ComposeInput(mode, title, instructions, link, body.BaseLink!.Trim());
                return null;
            default:
                return ValidationProblem("Modo inválido (use generate ou rewrite).");
        }
    }

    /// <summary>Nota própria (mesmo owner) ou snapshot publicado visível. Null = 404 (inclui nota privada de outra pessoa).</summary>
    private static async Task<BaseNote?> LoadBaseAsync(NoteDbContext db, string owner, ComposeInput input, CancellationToken ct)
    {
        switch (input.Link)
        {
            case NoteLink.Own own:
            {
                Note? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == own.NoteId && n.OwnerId == owner, ct);
                return note is null ? null : Checked(new BaseNote(note.Title.Value, NoteComposer.BaseText(note.Content), true, null, null, note.CreatedAt.Year));
            }
            case NoteLink.Public pub:
                return await FromPublicationAsync(db, owner, await PublicationReader.FindPublicAsync(db, pub.Handle, pub.Slug, ct), input.LinkUrl, ct);
            case NoteLink.Shared shared:
                return await FromPublicationAsync(db, owner, await PublicationReader.FindSharedAsync(db, shared.Token, ct), input.LinkUrl, ct);
            default:
                return null;
        }
    }

    private static async Task<BaseNote?> FromPublicationAsync(NoteDbContext db, string owner, Publication? publication, string? url, CancellationToken ct)
    {
        if (publication is null || await PublicationReader.CurrentRevisionAsync(db, publication, ct) is not { } revision)
        {
            return null;
        }

        string? author = await db.PublicProfiles.AsNoTracking().Where(p => p.UserId == publication.OwnerId)
            .Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        // Só usa o link como atribuição quando é uma URL http(s) absoluta (o cliente envia a URL completa).
        string? attributionUrl = url is not null && NoteContent.IsHttpUrl(url) ? url : null;
        return Checked(new BaseNote(revision.Title, NoteComposer.BaseText(revision.Content), publication.OwnerId == owner,
            attributionUrl, author, revision.PublishedAt.Year));
    }

    private static BaseNote Checked(BaseNote note) =>
        note.Text.Text.Length > NoteAiOrganizer.MaxInputChars
            ? throw new DomainException($"A nota base passa de {NoteAiOrganizer.MaxInputChars} caracteres.")
            : note;

    private static async Task<string> PromptAsync(NoteDbContext db, string owner, ComposeInput input, BaseNote? baseNote, CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().Where(c => c.OwnerId == owner).ToListAsync(ct);
        var tags = await db.Tags.AsNoTracking().Where(t => t.OwnerId == owner).ToListAsync(ct);
        return NoteComposer.UserPrompt(input.Mode, input.Title, input.Instructions, baseNote?.Title, baseNote?.Text, categories, tags);
    }

    private static async Task<IReadOnlyDictionary<string, ResolvedSource>> ResolveAllAsync(List<string> identifiers,
        SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        var found = new Dictionary<string, ResolvedSource>(StringComparer.OrdinalIgnoreCase);
        using var gate = new SemaphoreSlim(SourceLookupParallelism);
        var lookups = identifiers.Select(async identifier =>
        {
            await gate.WaitAsync(ct);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(SourceLookupTimeout);
                try
                {
                    return (identifier, source: await SourceResolutionEndpoints.TryResolveAsync(identifier, http, config, timeout.Token));
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return (identifier, source: (ResolvedSource?)null); // tempo esgotado = não conferida
                }
            }
            finally
            {
                gate.Release();
            }
        });

        foreach (var (identifier, source) in await Task.WhenAll(lookups))
        {
            if (source is not null)
            {
                found[identifier] = source;
            }
        }

        return found;
    }

    private static IResult BaseNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Nota não encontrada",
            detail: "A nota do link não existe, é privada de outra pessoa ou a publicação foi revogada.");

    private static IResult ValidationProblem(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Erro de validação", detail: detail);

    private static int MaxPerHour(IConfiguration config) => Math.Max(1, config.GetValue<int?>("Ai:ComposeMaxPerHour") ?? 10);

    private static async Task<UserPreferences> PreferencesAsync(NoteDbContext db, string owner, CancellationToken ct) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? UserPreferences.Default(owner);

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
