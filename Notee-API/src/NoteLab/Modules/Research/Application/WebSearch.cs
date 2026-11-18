namespace NoteLab.Application;

/// <summary>
/// Configuração <c>Research:WebSearch</c> da busca na web do DeepSeek (ADR-046). A nota mínima usa a escala
/// do buscador (rerank 0–3); no formato comum (<see cref="SourceHit.Score"/>) ela vale MinScore/3.
/// </summary>
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

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd();
}
