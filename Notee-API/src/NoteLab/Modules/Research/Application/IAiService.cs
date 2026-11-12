namespace NoteLab.Application;

/// <summary>Fonte citada pelo provider (título + URL http/https validada).</summary>
public sealed record AiSource(string Title, string Url);

/// <summary>Resposta do provider com as fontes que ele efetivamente devolveu e o uso reportado.</summary>
public sealed record AiAnswer(string Text, IReadOnlyList<AiSource> Sources, int? InputTokens = null, int? OutputTokens = null)
{
    public static AiAnswer WithoutSources(string text) => new(text, []);
}

/// <summary>Resposta de uma tarefa genérica (sem busca na web) com o uso reportado pelo provedor.</summary>
public sealed record AiCompletion(string Text, int? InputTokens = null, int? OutputTokens = null);

public interface IAiService
{
    public Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct);

    /// <summary>Providers sem suporte a fontes respondem sem fontes (nunca inventadas).</summary>
    public async Task<AiAnswer> AnswerAsync(string question, string exactQuote, CancellationToken ct) =>
        AiAnswer.WithoutSources(await GetAnswerAsync(question, exactQuote, ct));

    /// <summary>
    /// Tarefa genérica (ex.: organizar uma nota, ADR-037): instruções de sistema + conteúdo, sem ferramentas
    /// nem busca na web. Usado SOMENTE com consentimento da conta e confirmação explícita do envio.
    /// </summary>
    public Task<AiCompletion> CompleteAsync(string system, string user, int maxOutputTokens, CancellationToken ct) =>
        Task.FromException<AiCompletion>(new NotSupportedException("Este provedor não suporta tarefas genéricas."));
}
