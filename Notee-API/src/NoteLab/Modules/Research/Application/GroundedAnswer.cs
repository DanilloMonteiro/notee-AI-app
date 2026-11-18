using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NoteLab.Application;

/// <summary>
/// Resposta de dúvida embasada na busca: texto com [n] e só as fontes citadas (artigos/páginas reais).
/// <see cref="Text"/> null = pesquisou mas não achou nada bom: quem chama segue pelo caminho anterior, mas ainda
/// registra os tokens da consulta e as buscas feitas (custo e teto diário).
/// </summary>
public sealed record GroundedOutcome(string? Text, List<ResearchSourceDraft> Sources, int? InputTokens, int? OutputTokens,
    int WebSearches, int AcademicSearches = 0)
{
    public bool Answered => Text is not null;
}

/// <summary>
/// ADR-046/047 — o Notee pesquisa (artigos acadêmicos e/ou web) e o modelo responde SOMENTE com base nos resultados numerados.
/// Toda fonte gravada é um resultado da busca (artigo ou página real com link); marcador sem resultado é removido.
/// </summary>
public static partial class GroundedAnswer
{
    public const int MaxOutputTokens = 1_500;
    private const int MaxSummaryChars = 900;

    public const string System =
        "Você é um assistente de pesquisa integrado a um aplicativo de notas. Responda em português, de forma direta e concisa. " +
        "Use SOMENTE as informações dos resultados de busca numerados que vêm na mensagem (artigos acadêmicos e/ou páginas da web); não use conhecimento de fora para fatos, números ou datas. " +
        "Coloque [n] logo depois de cada afirmação, onde n é o número do resultado que a sustenta (pode usar mais de um, ex.: [1][3]). " +
        "Se os resultados não forem suficientes para responder, diga isso claramente e responda só o que eles sustentam. " +
        "Quando houver artigos acadêmicos (tipo \"artigo\"), prefira-os às páginas da web. " +
        "Responda SOMENTE com JSON: {\"answer\": string}.";

    /// <summary>Mensagem do usuário: pergunta, trecho e resultados numerados (tipo, título, autores, site/periódico, data, resumo).</summary>
    public static string User(string question, string exactQuote, IReadOnlyList<SourceHit> results) =>
        JsonSerializer.Serialize(new
        {
            pergunta = question,
            trechoSelecionado = string.IsNullOrWhiteSpace(exactQuote) ? null : exactQuote,
            resultados = results.Select((r, i) => new
            {
                n = i + 1,
                tipo = r.Origin == SourceOrigin.Academic ? "artigo" : "página",
                titulo = r.Title,
                autores = r.Authors.Count > 0 ? string.Join(", ", r.Authors.Take(3)) : null,
                site = r.SiteName,
                data = r.PublishedAt?.ToString("yyyy-MM-dd"),
                resumo = Clip(r.Excerpt, MaxSummaryChars),
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
    public static (string Text, List<ResearchSourceDraft> Sources) Finalize(string answer, IReadOnlyList<SourceHit> results)
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
            .Select(r => new ResearchSourceDraft(r.Title, r.Url, true, r.SiteName, r.PublishedAt,
                r.Origin == SourceOrigin.Web ? r.Authority : null, r.OriginWire, r.Doi, r.Authors, r.Trusted))
            .ToList();
        return (ResearchAnswerFormat.Renumber(answer, order), sources);
    }

    /// <summary>
    /// Consulta (pt + en pela IA, quando há artigos) → busca combinada → resposta embasada. Devolve null quando nada foi
    /// pesquisado (modo sem busca, origens indisponíveis) e um resultado sem texto quando pesquisou sem achar nada bom:
    /// nos dois casos quem chama segue pelo caminho anterior (referências declaradas, ADR-045).
    /// </summary>
    public static async Task<GroundedOutcome?> TryAnswerAsync(SourceSearchService search, SourceMode mode, SourceRules rules,
        bool allowAcademic, bool allowWeb, Func<string, string, int, CancellationToken, Task<AiCompletion>> complete,
        string question, string exactQuote, CancellationToken ct)
    {
        bool academic = SourceModes.IncludesAcademic(mode) && allowAcademic && search.AcademicAvailable;
        bool web = SourceModes.IncludesWeb(mode) && allowWeb && search.WebAvailable;
        if (!academic && !web)
        {
            return null;
        }

        // Palavras-chave em inglês só valem a chamada extra quando há busca de artigos.
        (SourceQuery query, AiCompletion? planUsage) = academic
            ? await SourceQueryPlanner.PlanAsync(complete, question, ct)
            : (SourceQueryPlanner.Plain(question), null);
        SourceSearchOutcome found = await search.SearchAsync(mode, query, rules, academic, web, ct);
        if (found.Hits.Count == 0)
        {
            return new GroundedOutcome(null, [], planUsage?.InputTokens, planUsage?.OutputTokens, found.WebCalls, found.AcademicCalls);
        }

        AiCompletion completion = await complete(System, User(question, exactQuote, found.Hits), MaxOutputTokens, ct);
        (string text, List<ResearchSourceDraft> sources) = Finalize(ParseAnswer(completion.Text), found.Hits);
        if (text.Length == 0)
        {
            text = "Não foi possível gerar uma resposta.";
        }

        return new GroundedOutcome(text, sources, Sum(planUsage?.InputTokens, completion.InputTokens),
            Sum(planUsage?.OutputTokens, completion.OutputTokens), found.WebCalls, found.AcademicCalls);
    }

    public static int? Sum(int? a, int? b) => a is null && b is null ? null : (a ?? 0) + (b ?? 0);

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"\[(\d{1,3})\]")]
    private static partial Regex Marker();
}
