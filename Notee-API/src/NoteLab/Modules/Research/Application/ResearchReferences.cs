namespace NoteLab.Application;

/// <summary>
/// Fonte da resposta pronta para gravar: conferida (com link) ou só declarada pela IA (sem link).
/// Site, data e autoridade vêm da busca na web (ADR-046), quando houver.
/// </summary>
/// Origem (web|academic), DOI, autores e "confiável" vêm da busca combinada (ADR-047).
public sealed record ResearchSourceDraft(string Title, string? Url, bool Verified,
    string? SiteName = null, DateTimeOffset? PublishedAt = null, int? Authority = null,
    string? Origin = null, string? Doi = null, IReadOnlyList<string>? Authors = null, bool Trusted = false);

/// <summary>
/// Junta as fontes de uma resposta de dúvida (ADR-045):
/// <list type="number">
/// <item>links devolvidos pela busca do provedor (Claude/Gemini) — conferidos pela própria busca;</item>
/// <item>referências DECLARADAS pela IA (provedores sem busca): DOI/ISBN/URL encontrado pelo resolvedor do Notee vira
/// fonte conferida com link; o resto vira "origem informada pela IA — não conferida", sem link.</item>
/// </list>
/// Os marcadores [n] do texto passam a apontar para a posição final de cada fonte.
/// </summary>
public static class ResearchReferences
{
    public static async Task<(string Text, List<ResearchSourceDraft> Sources)> AssembleAsync(
        AiAnswer answer, Func<string, CancellationToken, Task<ResolvedSource?>> resolve, CancellationToken ct)
    {
        var sources = answer.Sources.Select(s => new ResearchSourceDraft(s.Title, s.Url, true)).ToList();
        if (answer.DeclaredReferences.Count == 0)
        {
            return (answer.Text, sources);
        }

        var finalPosition = new Dictionary<int, int>();
        foreach (DeclaredReference reference in answer.DeclaredReferences)
        {
            ResolvedSource? resolved = reference.Identifier is { } identifier ? await resolve(identifier, ct) : null;
            string? link = resolved is null ? null : LinkFor(resolved);
            ResearchSourceDraft draft = link is not null
                ? new ResearchSourceDraft(string.IsNullOrWhiteSpace(resolved!.Title) ? reference.Label : resolved.Title, link, true)
                : new ResearchSourceDraft(reference.Label, null, false);

            // Mesma fonte citada com números diferentes: reaproveita a posição.
            int existing = sources.FindIndex(s =>
                (draft.Url is not null && string.Equals(s.Url, draft.Url, StringComparison.OrdinalIgnoreCase))
                || (draft.Url is null && s.Url is null && string.Equals(s.Title, draft.Title, StringComparison.OrdinalIgnoreCase)));
            if (existing < 0)
            {
                sources.Add(draft);
                existing = sources.Count - 1;
            }

            finalPosition[reference.N] = existing + 1;
        }

        return (ResearchAnswerFormat.Renumber(answer.Text, finalPosition), sources);
    }

    /// <summary>Link público da fonte encontrada: URL, senão DOI (doi.org), senão ISBN (Open Library).</summary>
    public static string? LinkFor(ResolvedSource resolved)
    {
        if (Http(resolved.Url) is { } url)
        {
            return url;
        }

        if (!string.IsNullOrWhiteSpace(resolved.Doi))
        {
            return $"https://doi.org/{Uri.EscapeDataString(resolved.Doi.Trim()).Replace("%2F", "/")}";
        }

        return string.IsNullOrWhiteSpace(resolved.Isbn) ? null : $"https://openlibrary.org/isbn/{Uri.EscapeDataString(resolved.Isbn.Trim())}";
    }

    private static string? Http(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
}
