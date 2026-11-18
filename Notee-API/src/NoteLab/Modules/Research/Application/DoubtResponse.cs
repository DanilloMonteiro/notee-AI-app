using NoteLab.Domain;

namespace NoteLab.Application;

/// <summary>BlockId/ExactQuote são nulos para dúvidas do mapa (sem âncora).</summary>
public record DoubtResponse(
    Guid Id,
    string Question,
    string? Answer,
    string Status,
    string? BlockId,
    string? ExactQuote,
    int StartIndex,
    int EndIndex
)
{
    public Guid NoteId { get; init; }
    public string Origin { get; init; } = nameof(DoubtOrigin.Selection);
    public string? AnswerProvider { get; init; }
    public string? Evidence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public ResearchJobResponse? LatestJob { get; init; }
    public List<SourceResponse> Sources { get; init; } = [];

    public static DoubtResponse From(Doubt d, ResearchJob? latestJob = null) =>
        new(d.Id, d.Question, d.Answer, d.Status.ToString(), d.Anchor?.BlockId, d.Anchor?.ExactQuote,
            d.Anchor?.StartIndex ?? 0, d.Anchor?.EndIndex ?? 0)
        {
            NoteId = d.NoteId,
            Origin = d.Origin.ToString(),
            AnswerProvider = d.AnswerProvider,
            Evidence = d.Evidence?.ToString(),
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            LatestJob = latestJob is null ? null : ResearchJobResponse.From(latestJob),
        };
}

public record ResearchJobResponse(
    Guid Id,
    Guid DoubtId,
    string Status,
    int Attempt,
    int MaxAttempts,
    DateTimeOffset NextAttemptAt,
    bool CancelRequested,
    string? Provider,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    /// <summary>Buscas na web feitas nesta execução (ADR-046).</summary>
    public int WebSearches { get; init; }
    /// <summary>Buscas de artigos acadêmicos feitas nesta execução (ADR-047).</summary>
    public int AcademicSearches { get; init; }

    public static ResearchJobResponse From(ResearchJob j) =>
        new(j.Id, j.DoubtId, j.Status.ToString(), j.Attempt, j.MaxAttempts, j.NextAttemptAt, j.CancelRequested,
            j.Provider, j.LastError, j.CreatedAt, j.CompletedAt)
        { InputTokens = j.InputTokens, OutputTokens = j.OutputTokens, WebSearches = j.WebSearches, AcademicSearches = j.AcademicSearches };
}

public record CreateBoardDoubtRequest(string Question);

/// <summary>
/// Fonte da resposta. <c>Verified = false</c>: origem informada pela IA e não conferida (sem link).
/// O número nos marcadores [n] da resposta é a posição na lista (1, 2, …).
/// </summary>
public record SourceResponse(string Title, string? Url, bool Verified = true)
{
    /// <summary>Dados do buscador (ADR-046): site, data de publicação e autoridade (-1 a 4).</summary>
    public string? SiteName { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public int? Authority { get; init; }
    /// <summary>ADR-047: "academic" (artigo) ou "web" (página); null em respostas antigas.</summary>
    public string? Origin { get; init; }
    public string? Doi { get; init; }
    /// <summary>Autores separados por "; ".</summary>
    public string? Authors { get; init; }
    public bool Trusted { get; init; }
}
