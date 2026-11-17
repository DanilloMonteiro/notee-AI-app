using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record PerspectiveGeneration(IReadOnlyList<PerspectiveSuggestion> Suggestions, bool InsufficientContext);

/// <summary>
/// Gerador local e determinístico de lentes e críticas (provedor "fake", sem IA externa).
/// Mesma nota → mesmas sugestões (categorias, textos e âncoras). Tudo é pergunta, nunca veredicto.
/// </summary>
public static partial class PerspectiveRules
{
    public const string Provider = "fake";
    public const int MaxLenses = 4;
    public const int MaxCritiques = 3;
    public const int MinWords = 15;
    private const int MaxQuoteLength = 280;

    private sealed record LensRule(string Category, string[] Keywords, string Question);
    private sealed record CritiqueRule(string Category, string[] Markers, string Question);
    private sealed record TextBlock(string Id, string Text);

    private static readonly string[] MathKeywords =
        ["derivada", "integral", "funcao", "limite", "equacao", "teorema", "matriz", "vetor", "calculo", "demonstracao", "polinomio", "logaritmo"];

    // Ordem da lista = desempate determinístico.
    private static readonly LensRule[] HumanitiesLenses =
    [
        new(PerspectiveCategories.Economic, ["economia", "economic", "pib", "inflacao", "juros", "mercado", "emprego", "fiscal", "renda", "preco"],
            "Como esse tema aparece pela lente econômica: quais custos, incentivos e efeitos distributivos estão em jogo?"),
        new(PerspectiveCategories.Political, ["governo", "politic", "eleicao", "partido", "congresso", "presidente", "poder", "estado", "impeachment"],
            "Como diferentes grupos políticos descreveriam esse tema, e como os defensores e os críticos de cada visão argumentariam?"),
        new(PerspectiveCategories.Legal, ["lei", "constituicao", "juridic", "tribunal", "direito", "crime", "norma", "stf", "legal"],
            "Que regras, normas ou decisões jurídicas se aplicam aqui, e onde há interpretações divergentes?"),
        new(PerspectiveCategories.Historical, ["historia", "historic", "seculo", "decada", "periodo", "antes", "origem", "epoca"],
            "Como esse tema chegou até aqui: que contexto histórico ou precedentes ajudam a entendê-lo?"),
        new(PerspectiveCategories.Social, ["sociedade", "social", "populacao", "desigualdade", "cultura", "comunidade", "pobreza"],
            "Quem é afetado por esse tema e como diferentes grupos sociais o vivenciam?"),
        new(PerspectiveCategories.Philosophical, ["etica", "moral", "justica", "liberdade", "verdade", "conceito", "filosof"],
            "Que pressupostos ou valores sustentam esse raciocínio, e como outra tradição de pensamento o questionaria?"),
    ];

    private static readonly LensRule[] MathLenses =
    [
        new(PerspectiveCategories.Geometric, ["reta", "tangente", "curva", "area", "grafico", "inclinacao", "plano", "angulo"],
            "Como esse resultado pode ser visto geometricamente: que figura, curva ou área o representa?"),
        new(PerspectiveCategories.Physical, ["velocidade", "aceleracao", "taxa", "movimento", "variacao", "tempo", "forca"],
            "Que fenômeno físico ou taxa de variação do mundo real esse conceito poderia descrever?"),
        new(PerspectiveCategories.Historical, ["newton", "leibniz", "euler", "historia", "seculo", "origem"],
            "Que problema histórico levou à criação desse conceito, e como ele foi formulado na época?"),
        new(PerspectiveCategories.Formal, ["definicao", "prova", "demonstracao", "teorema", "axioma", "hipotese", "regra"],
            "Qual é a definição formal por trás disso, e que hipóteses precisam valer para a regra funcionar?"),
    ];

    // Ordem da lista = prioridade quando há mais candidatos que MaxCritiques.
    private static readonly CritiqueRule[] CritiqueRules =
    [
        new(PerspectiveCategories.UnsupportedClaim, ["obviamente", "claramente", "e fato", "sem duvida", "comprovado", "todo mundo sabe", "evidentemente"],
            "Que evidência ou fonte sustenta essa afirmação?"),
        new(PerspectiveCategories.CorrelationAsCausation, ["por causa", "causou", "provocou", "levou a", "resultou", "graças a"],
            "Essa relação é de causa ou apenas aparecem juntas? Que outros fatores poderiam explicá-la?"),
        new(PerspectiveCategories.LogicalLeap, ["portanto", "logo", "por isso", "consequentemente", "assim sendo"],
            "Que passo liga as premissas anteriores a essa conclusão? Ele vale sempre?"),
        new(PerspectiveCategories.Generalization, ["todos", "todas", "sempre", "nunca", "ninguem", "nenhum", "qualquer"],
            "Há exceções ou contraexemplos para essa generalização?"),
        new(PerspectiveCategories.AmbiguousTerm, ["melhor", "pior", "eficiente", "justo", "correto", "normal", "bom", "ruim"],
            "O que exatamente esse termo significa aqui, e como ele poderia ser medido ou verificado?"),
    ];

    private static readonly string[] CounterMarkers =
        ["porem", "entretanto", "contudo", "todavia", "por outro lado", "criticos", "no entanto", "discordam", "contraponto"];

    private const string OneSidedQuestion = "Como alguém que discorda dessa visão argumentaria, e que evidência essa pessoa usaria?";

    public static PerspectiveGeneration Generate(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        List<TextBlock> blocks = ReadBlocks(note);
        string body = blocks.Count > 0 ? string.Join('\n', blocks.Select(b => b.Text)) : note.PlainText ?? note.Content ?? "";
        string folded = Fold($"{note.Title.Value}\n{body}");

        if (WordCount(body) < MinWords)
        {
            return new([], true);
        }

        var suggestions = new List<PerspectiveSuggestion>();
        foreach (LensRule lens in PickLenses(folded))
        {
            suggestions.Add(new PerspectiveSuggestion(note, PerspectiveKind.Lens, lens.Category, lens.Question, null, Provider));
        }

        foreach ((string category, string question, DoubtAnchor anchor) in PickCritiques(blocks, folded))
        {
            suggestions.Add(new PerspectiveSuggestion(note, PerspectiveKind.Critique, category, question, anchor, Provider));
        }

        return new(suggestions, false);
    }

    private static IEnumerable<LensRule> PickLenses(string folded)
    {
        bool isMath = MathKeywords.Count(k => ContainsWord(folded, k)) >= 2;
        LensRule[] rules = isMath ? MathLenses : HumanitiesLenses;
        // Lentes com mais termos presentes primeiro; empate mantém a ordem fixa da lista.
        return rules
            .Select((rule, index) => (rule, index, score: rule.Keywords.Count(k => ContainsPrefix(folded, k))))
            .OrderByDescending(x => x.score).ThenBy(x => x.index)
            .Take(MaxLenses)
            .Select(x => x.rule);
    }

    private static List<(string Category, string Question, DoubtAnchor Anchor)> PickCritiques(List<TextBlock> blocks, string folded)
    {
        var result = new List<(string, string, DoubtAnchor)>();
        if (blocks.Count == 0)
        {
            return result; // conteúdo legado sem IDs de bloco: não há como ancorar
        }

        var sentences = blocks.SelectMany(b => Sentences(b)).ToList();
        foreach (CritiqueRule rule in CritiqueRules)
        {
            if (result.Count == MaxCritiques)
            {
                break;
            }

            foreach (var (block, start, text) in sentences)
            {
                if (rule.Markers.Any(m => ContainsWord(Fold(text), m)))
                {
                    result.Add((rule.Category, rule.Question, Anchor(block, start, text)));
                    break;
                }
            }
        }

        if (result.Count < MaxCritiques && !CounterMarkers.Any(m => ContainsWord(folded, m)) && sentences.Count > 0)
        {
            var (block, start, text) = sentences[0];
            result.Add((PerspectiveCategories.OneSided, OneSidedQuestion, Anchor(block, start, text)));
        }

        return result;
    }

    private static DoubtAnchor Anchor(TextBlock block, int start, string sentence)
    {
        string quote = sentence.Length > MaxQuoteLength ? sentence[..MaxQuoteLength] : sentence;
        return new DoubtAnchor(block.Id, quote, start, start + quote.Length);
    }

    /// <summary>Frases do bloco com posição exata no texto original (para a âncora bater com o editor).</summary>
    private static IEnumerable<(TextBlock Block, int Start, string Text)> Sentences(TextBlock block)
    {
        foreach (Match m in SentenceRegex().Matches(block.Text))
        {
            string raw = m.Value;
            int lead = raw.Length - raw.TrimStart().Length;
            string text = raw.Trim();
            if (text.Length > 0)
            {
                yield return (block, m.Index + lead, text);
            }
        }
    }

    private static List<TextBlock> ReadBlocks(Note note)
    {
        var blocks = new List<TextBlock>();
        if (string.IsNullOrWhiteSpace(note.Content) || !note.Content.TrimStart().StartsWith('['))
        {
            return blocks;
        }

        try
        {
            using var doc = JsonDocument.Parse(note.Content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return blocks;
            }

            foreach (JsonElement block in doc.RootElement.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String
                    || !block.TryGetProperty("text", out JsonElement text) || text.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string type = block.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                if (type is "chart" or "table" or "code" || string.IsNullOrWhiteSpace(text.GetString()))
                {
                    continue;
                }

                blocks.Add(new TextBlock(id.GetString()!, text.GetString()!));
            }
        }
        catch (JsonException)
        {
            // Texto puro que começa com "[": tratado como conteúdo legado.
        }

        return blocks;
    }

    private static int WordCount(string text) => WordRegex().Count(text);

    private static bool ContainsWord(string folded, string term) =>
        Regex.IsMatch(folded, $@"(?<![a-z0-9]){Regex.Escape(Fold(term))}(?![a-z0-9])");

    private static bool ContainsPrefix(string folded, string term) =>
        Regex.IsMatch(folded, $@"(?<![a-z0-9]){Regex.Escape(Fold(term))}");

    private static string Fold(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"[^.!?\n]+[.!?]*")]
    private static partial Regex SentenceRegex();

    [GeneratedRegex(@"\w+")]
    private static partial Regex WordRegex();
}
