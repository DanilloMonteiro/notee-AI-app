namespace NoteLab.Application;

/// <summary>
/// Resultado da busca na web (ADR-046). Página real devolvida pelo buscador, com link.
/// <see cref="RerankScore"/>: 0–3 (relevância + autoridade + atualidade); <see cref="Authority"/>: -1 (desconhecida) a 4.
/// </summary>
public sealed record WebSearchResult(
    string Title,
    string Url,
    string? SiteName,
    string Snippet,
    string Summary,
    DateTimeOffset? PublishedAt,
    int Authority,
    double RerankScore);

/// <summary>Falha da busca (sem saldo, chave inválida, limite, indisponível). Quem chama cai no caminho sem busca.</summary>
public sealed class WebSearchException(string message, int? status = null) : Exception(message)
{
    public int? Status { get; } = status;
}

/// <summary>Busca na web usada para embasar respostas e notas. Só a pergunta/título sai do Notee.</summary>
public interface IWebSearch
{
    /// <summary>Há chave configurada para o buscador.</summary>
    public bool Available { get; }

    /// <summary>
    /// <paramref name="query"/>: até 128 caracteres (operadores como lang: incluídos).
    /// <paramref name="intent"/>: pergunta original, até 128 caracteres, para o buscador entender a intenção.
    /// </summary>
    public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, string? intent, CancellationToken ct);
}

/// <summary>Configuração <c>Research:WebSearch</c>.</summary>
public sealed record WebSearchOptions(
    bool Enabled,
    double MinScore,
    int MaxResults,
    int MaxPerSite,
    int MaxPerDay,
    decimal PricePer1000Usd)
{
    public const int MaxQueryLength = 128;

    public static WebSearchOptions From(IConfiguration config) => new(
        config.GetValue<bool?>("Research:WebSearch:Enabled") ?? true,
        config.GetValue<double?>("Research:WebSearch:MinScore") ?? 1.5,
        Math.Clamp(config.GetValue<int?>("Research:WebSearch:MaxResults") ?? 6, 1, 10),
        Math.Clamp(config.GetValue<int?>("Research:WebSearch:MaxPerSite") ?? 2, 1, 10),
        Math.Max(0, config.GetValue<int?>("Research:WebSearch:MaxPerDay") ?? 500),
        config.GetValue<decimal?>("Research:WebSearch:PricePer1000Usd") ?? 0.75m);

    /// <summary>Custo de N buscas cobradas (a cobrança só acontece quando a busca devolve resultados).</summary>
    public decimal CostOf(int searches) => Math.Round(searches * PricePer1000Usd / 1000m, 6);
}

public static class WebSearchQuery
{
    private const string LanguageFilter = " lang:pt,en";

    /// <summary>
    /// Consulta da busca: o texto (pergunta ou título + descrição), cortado para caber em 128 caracteres
    /// junto com o filtro de idioma (português e inglês). Nunca inclui a nota inteira nem dados da conta.
    /// </summary>
    public static (string Query, string Intent) Build(string text)
    {
        string clean = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string intent = Clip(clean, WebSearchOptions.MaxQueryLength);
        string query = Clip(clean, WebSearchOptions.MaxQueryLength - LanguageFilter.Length) + LanguageFilter;
        return (query, intent);
    }

    /// <summary>
    /// Resultados usados: nota mínima, no máximo <see cref="WebSearchOptions.MaxPerSite"/> por site,
    /// ordenados pela nota, sem URL repetida e com resumo para a IA ler.
    /// </summary>
    public static List<WebSearchResult> Select(IEnumerable<WebSearchResult> results, WebSearchOptions options)
    {
        var picked = new List<WebSearchResult>();
        var perSite = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WebSearchResult r in results.Where(r => r.RerankScore >= options.MinScore).OrderByDescending(r => r.RerankScore))
        {
            bool parsed = Uri.TryCreate(r.Url, UriKind.Absolute, out Uri? uri);
            string site = parsed ? uri!.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host : r.Url;
            // Mesma página com e sem "www." ou com barra final conta uma vez só.
            string pageKey = parsed ? $"{site}{uri!.PathAndQuery.TrimEnd('/')}" : r.Url;
            if ((string.IsNullOrWhiteSpace(r.Summary) && string.IsNullOrWhiteSpace(r.Snippet)) || !urls.Add(pageKey))
            {
                continue;
            }

            if (perSite.GetValueOrDefault(site) >= options.MaxPerSite)
            {
                continue;
            }

            perSite[site] = perSite.GetValueOrDefault(site) + 1;
            picked.Add(r);
            if (picked.Count == options.MaxResults)
            {
                break;
            }
        }

        return picked;
    }

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd();
}
