using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoteLab.Application;

/// <summary>Resultado da busca combinada: artigos primeiro, depois a web; chamadas feitas para custo e teto diário.</summary>
public sealed record SourceSearchOutcome(List<SourceHit> Hits, int WebCalls, int AcademicCalls, List<string> Notices)
{
    public int AcademicCount => Hits.Count(h => h.Origin == SourceOrigin.Academic);

    public int WebCount => Hits.Count(h => h.Origin == SourceOrigin.Web);
}

/// <summary>
/// ADR-047 — combina as origens de fonte. Em <see cref="SourceMode.Mixed"/> consulta os artigos (OpenAlex) e só
/// vai à internet quando vierem menos que <see cref="SourceSearchOptions.MinAcademicForArticlesOnly"/> artigos bons,
/// completando o que falta. Falha de uma origem não derruba a outra.
/// </summary>
public sealed class SourceSearchService(IEnumerable<ISourceSearchProvider> providers, IConfiguration config)
{
    public SourceSearchOptions Options { get; } = SourceSearchOptions.From(config);

    public WebSearchOptions WebOptions { get; } = WebSearchOptions.From(config);

    private ISourceSearchProvider? Academic => providers.FirstOrDefault(p => p.Origin == SourceOrigin.Academic && p.Available);

    private ISourceSearchProvider? Web => providers.FirstOrDefault(p => p.Origin == SourceOrigin.Web && p.Available);

    public bool AcademicAvailable => Options.AcademicEnabled && Academic is not null;

    public bool WebAvailable => WebOptions.Enabled && Web is not null;

    /// <param name="allowAcademic">Consentimento e teto diário do OpenAlex.</param>
    /// <param name="allowWeb">Busca web permitida (DeepSeek ativo, consentimento e teto diário).</param>
    public async Task<SourceSearchOutcome> SearchAsync(SourceMode mode, SourceQuery query, SourceRules rules,
        bool allowAcademic, bool allowWeb, CancellationToken ct)
    {
        var hits = new List<SourceHit>();
        var notices = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int academicCalls = 0;
        int webCalls = 0;

        if (SourceModes.IncludesAcademic(mode) && allowAcademic && AcademicAvailable)
        {
            ISourceSearchProvider academic = Academic!;
            var found = new List<SourceHit>();
            try
            {
                found.AddRange(await academic.SearchAsync(query, ct));
                academicCalls++;
                // Poucos artigos com os termos em inglês: tenta também em português (SciELO e periódicos nacionais).
                if (found.Count < Options.MaxResults && !string.Equals(query.Pt, query.En, StringComparison.OrdinalIgnoreCase))
                {
                    found.AddRange(await academic.SearchAsync(query with { En = query.Pt }, ct));
                    academicCalls++;
                }
            }
            catch (SourceSearchException ex)
            {
                notices.Add(ex.Message);
            }

            // DOIs são todos "doi.org": não limitar por site entre artigos.
            hits.AddRange(SourceMix.Select(found, Options.AcademicMinScore, Options.MaxResults, Options.MaxResults, rules, seen));
        }

        bool needWeb = mode == SourceMode.WebOnly || (mode == SourceMode.Mixed && hits.Count < Options.MinAcademicForArticlesOnly);
        if (needWeb && allowWeb && WebAvailable)
        {
            int take = Options.MaxResults - hits.Count;
            try
            {
                IReadOnlyList<SourceHit> found = await Web!.SearchAsync(query, ct);
                webCalls = found.Count > 0 ? 1 : 0; // o DeepSeek só cobra busca com resultado
                hits.AddRange(SourceMix.Select(found, WebOptions.MinScore / 3.0, WebOptions.MaxPerSite, take, rules, seen));
            }
            catch (SourceSearchException ex)
            {
                notices.Add(ex.Message);
            }
        }

        return new SourceSearchOutcome(hits, webCalls, academicCalls, notices);
    }
}

/// <summary>
/// Prepara a consulta: uma chamada curta à IA já autorizada transforma a pergunta em palavras-chave em
/// português e em inglês. Sem resposta válida, usa o texto original nos dois idiomas.
/// </summary>
public static class SourceQueryPlanner
{
    public const int MaxOutputTokens = 120;
    private const int MaxQueryLength = 120;

    public const string System =
        "Você prepara consultas para bases de artigos científicos e buscadores. A partir do texto recebido, " +
        "escreva de 3 a 8 palavras-chave que capturem o assunto central (sem perguntas, sem artigos e preposições desnecessários), " +
        "uma versão em português e outra em inglês. Responda SOMENTE com JSON: {\"pt\": string, \"en\": string}.";

    /// <summary>Consulta sem IA: o texto original (até 120 caracteres) nos dois idiomas.</summary>
    public static SourceQuery Plain(string text)
    {
        string clean = Clean(text);
        string intent = Clip(clean, WebSearchOptions.MaxQueryLength);
        return new SourceQuery(Clip(clean, MaxQueryLength), Clip(clean, MaxQueryLength), intent);
    }

    /// <summary>Lê {"pt","en"}; campo ausente ou vazio cai no texto original.</summary>
    public static SourceQuery Parse(string? raw, string text)
    {
        SourceQuery plain = Plain(text);
        string body = (raw ?? "").Trim();
        int open = body.IndexOf('{');
        int close = body.LastIndexOf('}');
        if (open < 0 || close <= open)
        {
            return plain;
        }

        try
        {
            if (JsonNode.Parse(body[open..(close + 1)]) is not JsonObject root)
            {
                return plain;
            }

            string pt = Clip(Clean(Str(root, "pt")), MaxQueryLength);
            string en = Clip(Clean(Str(root, "en")), MaxQueryLength);
            return new SourceQuery(pt.Length > 0 ? pt : plain.Pt, en.Length > 0 ? en : plain.En, plain.Intent);
        }
        catch (JsonException)
        {
            return plain;
        }
    }

    /// <summary>Gera a consulta com a IA; falha do provedor = consulta com o texto original e sem custo.</summary>
    public static async Task<(SourceQuery Query, AiCompletion? Usage)> PlanAsync(
        Func<string, string, int, CancellationToken, Task<AiCompletion>> complete, string text, CancellationToken ct)
    {
        try
        {
            AiCompletion completion = await complete(System, JsonSerializer.Serialize(new { texto = Clip(Clean(text), 600) }), MaxOutputTokens, ct);
            return (Parse(completion.Text, text), completion);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException
                                       or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return (Plain(text), null);
        }
    }

    private static string Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s ?? "" : "";

    private static string Clean(string? text) => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd();
}
