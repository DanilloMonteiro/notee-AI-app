namespace NoteLab.Application;

/// <summary>Fonte citada pelo provider (título + URL http/https validada).</summary>
public sealed record AiSource(string Title, string Url);

/// <summary>Resposta do provider com as fontes que ele efetivamente devolveu e o uso reportado.</summary>
public sealed record AiAnswer(string Text, IReadOnlyList<AiSource> Sources, int? InputTokens = null, int? OutputTokens = null)
{
    public static AiAnswer WithoutSources(string text) => new(text, []);
}

public interface IAiService
{
    public Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct);

    /// <summary>Providers sem suporte a fontes respondem sem fontes (nunca inventadas).</summary>
    public async Task<AiAnswer> AnswerAsync(string question, string exactQuote, CancellationToken ct) =>
        AiAnswer.WithoutSources(await GetAnswerAsync(question, exactQuote, ct));
}
