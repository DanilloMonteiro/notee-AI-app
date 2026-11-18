using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NoteLab.Application;

/// <summary>Resposta de dúvida embasada na busca: texto com [n] e só as fontes citadas (todas páginas reais).</summary>
public sealed record GroundedOutcome(string Text, List<ResearchSourceDraft> Sources, int? InputTokens, int? OutputTokens, int WebSearches);

/// <summary>
/// ADR-046 — o Notee pesquisa na web e o modelo responde SOMENTE com base nos resultados numerados.
/// Toda fonte gravada é um resultado da busca (página real com link); marcador sem resultado é removido.
/// </summary>
public static partial class GroundedAnswer
{
    public const int MaxOutputTokens = 1_500;
    private const int MaxSummaryChars = 900;

    public const string System =
        "Você é um assistente de pesquisa integrado a um aplicativo de notas. Responda em português, de forma direta e concisa. " +
        "Use SOMENTE as informações dos resultados de busca numerados que vêm na mensagem; não use conhecimento de fora para fatos, números ou datas. " +
        "Coloque [n] logo depois de cada afirmação, onde n é o número do resultado que a sustenta (pode usar mais de um, ex.: [1][3]). " +
        "Se os resultados não forem suficientes para responder, diga isso claramente e responda só o que eles sustentam. " +
        "Responda SOMENTE com JSON: {\"answer\": string}.";

    /// <summary>Mensagem do usuário: pergunta, trecho e resultados numerados (título, site, data, resumo).</summary>
    public static string User(string question, string exactQuote, IReadOnlyList<WebSearchResult> results) =>
        JsonSerializer.Serialize(new
        {
            pergunta = question,
            trechoSelecionado = string.IsNullOrWhiteSpace(exactQuote) ? null : exactQuote,
            resultados = results.Select((r, i) => new
            {
                n = i + 1,
                titulo = r.Title,
                site = r.SiteName,
                data = r.PublishedAt?.ToString("yyyy-MM-dd"),
                resumo = Clip(string.IsNullOrWhiteSpace(r.Summary) ? r.Snippet : r.Summary, MaxSummaryChars),
            }),
        });

    /// <summary>Texto da resposta (JSON {"answer"} ou, se o modelo não respeitar o formato, o texto como veio).</summary>
    public static string ParseAnswer(string raw)
    {
        string body = (raw ?? "").Trim();
        int open = body.IndexOf('{');
        int close = body.LastIndexOf('}');
        if (open >= 0 && close > open)
        {
            try
            {
                if (JsonNode.Parse(body[open..(close + 1)]) is JsonObject root
                    && root["answer"] is JsonValue v && v.TryGetValue(out string? answer) && !string.IsNullOrWhiteSpace(answer))
                {
                    return answer.Trim();
                }
            }
            catch (JsonException)
            {
                // cai no texto puro
            }
        }

        return body;
    }

    /// <summary>
    /// Fica só com os resultados citados, na ordem da primeira citação, e renumera [n] para 1, 2, 3…
    /// Marcadores que não correspondem a um resultado são removidos.
    /// </summary>
    public static (string Text, List<ResearchSourceDraft> Sources) Finalize(string answer, IReadOnlyList<WebSearchResult> results)
    {
        var order = new Dictionary<int, int>();
        foreach (Match m in Marker().Matches(answer))
        {
            if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n <= results.Count && !order.ContainsKey(n))
            {
                order[n] = order.Count + 1;
            }
        }

        var sources = order.OrderBy(kv => kv.Value)
            .Select(kv => results[kv.Key - 1])
            .Select(r => new ResearchSourceDraft(r.Title, r.Url, true, r.SiteName, r.PublishedAt, r.Authority))
            .ToList();
        return (ResearchAnswerFormat.Renumber(answer, order), sources);
    }

    /// <summary>
    /// Busca → seleção → resposta embasada. Devolve null quando não há como embasar (busca indisponível,
    /// falhou ou sem resultado bom): quem chama segue pelo caminho anterior (referências declaradas, ADR-045).
    /// </summary>
    public static async Task<GroundedOutcome?> TryAnswerAsync(IWebSearch search, WebSearchOptions options,
        Func<string, string, int, CancellationToken, Task<AiCompletion>> complete,
        string question, string exactQuote, CancellationToken ct)
    {
        if (!options.Enabled || !search.Available)
        {
            return null;
        }

        (string query, string intent) = WebSearchQuery.Build(question);
        IReadOnlyList<WebSearchResult> found;
        try
        {
            found = await search.SearchAsync(query, intent, ct);
        }
        catch (WebSearchException)
        {
            return null;
        }

        List<WebSearchResult> selected = WebSearchQuery.Select(found, options);
        if (selected.Count == 0)
        {
            return null;
        }

        AiCompletion completion = await complete(System, User(question, exactQuote, selected), MaxOutputTokens, ct);
        (string text, List<ResearchSourceDraft> sources) = Finalize(ParseAnswer(completion.Text), selected);
        if (text.Length == 0)
        {
            text = "Não foi possível gerar uma resposta.";
        }

        // A busca é cobrada quando devolve resultados (found não vazio), mesmo que nenhum passe no filtro.
        return new GroundedOutcome(text, sources, completion.InputTokens, completion.OutputTokens, found.Count > 0 ? 1 : 0);
    }

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"\[(\d{1,3})\]")]
    private static partial Regex Marker();
}
