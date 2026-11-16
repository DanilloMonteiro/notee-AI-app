using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record AiSourceCheckRequest(bool Confirm);
public static class SourceVerificationEndpoints
{
    private const string SystemPrompt = "SOURCE-CITATION-CHECK. Avalie se a afirmação é apoiada pelo trecho da fonte. Responda somente JSON: {\"verdict\":\"supports|partially|contradicts|insufficient\",\"arguments\":[{\"point\":\"...\",\"sourceExcerpt\":\"cópia literal do trecho recebido\"}]}. Não declare verdade/falsidade nem percentuais. Não invente citações.";

    public static void MapSourceVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var citations = app.MapGroup("/citations").RequireAuthorization().WithTags("Source checks");
        citations.MapGet("/{id:guid}/checks", ListAsync);
        citations.MapPost("/{id:guid}/checks", RetryAsync);
        citations.MapPost("/{id:guid}/ai-check/estimate", EstimateAiAsync);
        citations.MapPost("/{id:guid}/ai-check", RunAiAsync);
    }

    private static async Task<IResult> ListAsync(Guid id, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        if (!await db.SourceCitations.AnyAsync(c => c.Id == id && c.OwnerId == owner, ct)) return Results.NotFound();
        var rows = await db.SourceChecks.AsNoTracking().Where(c => c.CitationId == id && c.OwnerId == owner)
            .OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync(ct);
        return Results.Ok(rows.Select(ToResponse));
    }

    private static async Task<IResult> RetryAsync(Guid id, NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        SourceCitation? citation = await db.SourceCitations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == owner, ct);
        if (citation is null) return Results.NotFound();
        int limit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
        if (await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) >= limit)
            return Results.Problem(statusCode: 429, title: "Cota de verificação atingida", detail: $"Limite de {limit} verificações por dia atingido.");
        var check = new SourceCheck(owner, citation.SourceId, SourceCheckLayer.Citation, id);
        db.SourceChecks.Add(check); db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/citations/{id}/checks", new { checkId = check.Id, status = "pending" });
    }

    private static async Task<IResult> EstimateAiAsync(Guid id, NoteDbContext db, ClaimsPrincipal user,
        AiProviderSelector selector, SourceArchiveStorage storage, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        var material = await LoadMaterialAsync(db, id, owner, storage, ct);
        if (material is null) return Results.NotFound();
        UserPreferences prefs = await PreferencesAsync(db, owner, ct);
        string provider = selector.Describe(prefs);
        int inputTokens = Math.Max(1, (material.Value.Claim.Length + material.Value.Excerpt.Length + 1200) / 4);
        int outputTokens = 500;
        int limit = Math.Max(1, config.GetValue<int?>("Ai:SourceCheckMaxPerHour") ?? 5);
        int used = await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.Layer == SourceCheckLayer.Ai && c.CreatedAt >= DateTimeOffset.UtcNow.AddHours(-1), ct);
        return Results.Ok(new { provider, external = provider != "fake", consentGiven = prefs.AllowsExternalAi,
            hasArchive = material.Value.HasArchive, estimatedInputTokens = inputTokens, estimatedOutputTokens = outputTokens,
            estimatedCostUsd = provider == "fake" ? 0m : AiPricing.Cost(config, provider, inputTokens, outputTokens),
            maxPerHour = limit, remainingThisHour = Math.Max(0, limit - used), maxSourceExcerptCharacters = 6000,
            claim = material.Value.Claim, sourceExcerpt = material.Value.Excerpt });
    }

    private static async Task<IResult> RunAiAsync(Guid id, AiSourceCheckRequest body, NoteDbContext db, ClaimsPrincipal user,
        AiProviderSelector selector, SourceArchiveStorage storage, IConfiguration config, CancellationToken ct)
    {
        if (!body.Confirm) return Results.Problem(statusCode: 400, title: "Confirmação necessária", detail: "Confirme depois de revisar a estimativa e o trecho que será enviado.");
        string owner = Owner(user);
        var material = await LoadMaterialAsync(db, id, owner, storage, ct);
        if (material is null) return Results.NotFound();
        SourceCitation citation = material.Value.Citation;
        if (!material.Value.HasArchive)
        {
            var unavailable = new SourceCheck(owner, citation.SourceId, SourceCheckLayer.Ai, id);
            unavailable.Complete(SourceCheckStatus.Unverifiable, "Sem texto arquivado; nenhuma chamada de IA foi feita.", Array.Empty<object>(), "none", verdict: "insufficient");
            db.SourceChecks.Add(unavailable); await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(unavailable));
        }
        int limit = Math.Max(1, config.GetValue<int?>("Ai:SourceCheckMaxPerHour") ?? 5);
        DateTimeOffset since = DateTimeOffset.UtcNow.AddHours(-1);
        if (await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.Layer == SourceCheckLayer.Ai && c.CreatedAt >= since, ct) >= limit)
            return Results.Problem(statusCode: 429, title: "Cota de análise por IA atingida", detail: $"Limite de {limit} análises por hora.");
        UserPreferences prefs = await PreferencesAsync(db, owner, ct);
        IAiService ai = selector.For(prefs);
        string provider = selector.Describe(prefs);
        string payload = JsonSerializer.Serialize(new { claim = material.Value.Claim, noteContext = material.Value.Context, sourceExcerpt = material.Value.Excerpt });
        AiCompletion completion;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { completion = await ai.CompleteAsync(SystemPrompt, payload, 900, timeout.Token); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException or OperationCanceledException && !ct.IsCancellationRequested)
        { return Results.Problem(statusCode: 502, title: "Falha no provedor de IA", detail: $"A checagem não foi gravada ({ex.GetType().Name})."); }
        AiSourceCheckResult result;
        try { result = SourceAiResponseValidator.Parse(completion.Text, material.Value.Excerpt); }
        catch (InvalidAiSourceCheckException ex)
        { return Results.Problem(statusCode: 422, title: "Resposta da IA recusada", detail: ex.Message); }
        SourceCheckStatus status = result.Verdict switch
        {
            "supports" => SourceCheckStatus.Confirmed,
            "partially" => SourceCheckStatus.FoundWithDifferences,
            "contradicts" => SourceCheckStatus.Mismatch,
            _ => SourceCheckStatus.Unverifiable
        };
        var check = new SourceCheck(owner, citation.SourceId, SourceCheckLayer.Ai, id);
        decimal? cost = provider == "fake" ? 0m : AiPricing.Cost(config, provider, completion.InputTokens, completion.OutputTokens);
        check.Complete(status, $"Análise assistida por IA: {result.Verdict}.", result.Arguments.Select(a => new SourceCheckArgument(a.Point, a.SourceExcerpt)).ToList(),
            provider, completion.InputTokens, completion.OutputTokens, cost, result.Verdict);
        db.SourceChecks.Add(check);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(check));
    }

    private static async Task<(SourceCitation Citation, string Claim, string Context, string Excerpt, bool HasArchive)?> LoadMaterialAsync(
        NoteDbContext db, Guid id, string owner, SourceArchiveStorage storage, CancellationToken ct)
    {
        SourceCitation? citation = await db.SourceCitations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == owner, ct);
        if (citation is null) return null;
        Note? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == citation.NoteId && n.OwnerId == owner, ct);
        string context = NoteContext(note?.Content, citation);
        List<SourceArchive> rows = await db.SourceArchives.AsNoTracking().Where(a => a.OwnerId == owner && a.SourceId == citation.SourceId && a.Kind == "file")
            .OrderByDescending(a => a.CreatedAt).Take(1).ToListAsync(ct);
        if (rows.Count == 0) return (citation, citation.ExactQuote ?? citation.SourceQuote ?? context, context, "", false);
        // Archive extraction uses private per-owner objects; no archive path or signed URL is exposed to the provider.
        string excerpt = await ReadExcerptAsync(storage, rows[0], citation, ct);
        return (citation, citation.ExactQuote ?? context, context, excerpt, !string.IsNullOrWhiteSpace(excerpt));
    }

    private static async Task<string> ReadExcerptAsync(SourceArchiveStorage storage, SourceArchive archive, SourceCitation citation, CancellationToken ct)
    {
        List<ExtractedPage> pages = await storage.ReadTextAsync(archive, ct);
        IEnumerable<ExtractedPage> selected = citation.PageStart.HasValue
            ? pages.Where(p => p.Page >= citation.PageStart && p.Page <= (citation.PageEnd ?? citation.PageStart)) : pages;
        string text = string.Join("\n", selected.Select(p => $"[Página {p.Page}] {p.Text}"));
        if (text.Length <= 6000) return text;
        string anchor = citation.SourceQuote ?? "";
        int at = string.IsNullOrWhiteSpace(anchor) ? 0 : text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
        int start = Math.Max(0, at - 2500);
        return text.Substring(start, Math.Min(6000, text.Length - start));
    }

    private static string NoteContext(string? content, SourceCitation citation)
    {
        if (string.IsNullOrWhiteSpace(content)) return citation.ExactQuote ?? "";
        try
        {
            using JsonDocument json = JsonDocument.Parse(content);
            foreach (JsonElement block in json.RootElement.EnumerateArray())
                if (block.TryGetProperty("id", out JsonElement id) && id.GetString() == citation.BlockId && block.TryGetProperty("text", out JsonElement text))
                {
                    string value = text.GetString() ?? "";
                    int start = Math.Clamp(citation.StartIndex ?? 0, 0, value.Length);
                    int end = Math.Clamp(citation.EndIndex ?? start, start, value.Length);
                    return value.Substring(Math.Max(0, start - 400), Math.Min(value.Length, end + 400) - Math.Max(0, start - 400));
                }
        }
        catch (JsonException) { }
        return citation.ExactQuote ?? "";
    }

    private static async Task<UserPreferences> PreferencesAsync(NoteDbContext db, string owner, CancellationToken ct) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? UserPreferences.Default(owner);
    private static object ToResponse(SourceCheck c) => new { c.Id, layer = c.Layer.ToString().ToLowerInvariant(), status = c.Status.ToString().ToLowerInvariant(), c.Verdict,
        c.Summary, arguments = JsonSerializer.Deserialize<List<SourceCheckArgument>>(c.ArgumentsJson) ?? [], c.Provider, c.InputTokens, c.OutputTokens, c.CostUsd, c.CheckedAt };
    private static string Owner(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
