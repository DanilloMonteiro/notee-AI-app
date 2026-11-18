using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NoteLab.Application;

/// <summary>
/// Referência declarada pela IA para um trecho da resposta (ADR-045). Ainda NÃO conferida:
/// o worker confere <see cref="Identifier"/> (DOI/ISBN/URL) e, se não encontrar, guarda só o rótulo como
/// "origem informada pela IA — não conferida", sem link.
/// </summary>
public sealed record DeclaredReference(int N, string? Identifier, string Label);

/// <summary>
/// Resposta de dúvida para provedores SEM busca na web (ex.: DeepSeek): a IA marca trechos com [1], [2]
/// e declara de onde veio cada informação. Nada aqui é tratado como verdade; só o worker marca como conferido.
/// </summary>
public static partial class ResearchAnswerFormat
{
    public const int MaxReferences = 8;
    public const int MaxLabelLength = 300;
    public const int MaxIdentifierLength = 2048;

    public const string System =
        "Você é um assistente de pesquisa integrado a um aplicativo de notas. Responda em português, de forma direta e concisa. " +
        "Responda SOMENTE com JSON no formato {\"answer\": string, \"references\": [{\"n\": number, \"identifier\": string|null, \"label\": string}]}. " +
        "Em \"answer\", coloque [1], [2]… logo depois de cada afirmação que veio de uma referência. " +
        "Em \"references\", \"identifier\" é um DOI, ISBN ou URL que você tem certeza de que existe (senão null) " +
        "e \"label\" é o nome legível da origem (ex.: \"IBGE, Censo 2022\"). " +
        "Não invente fontes, números nem citações; se não souber de onde veio, não marque a afirmação. " +
        "Se não houver evidência suficiente, diga isso claramente.";

    /// <summary>
    /// Lê a resposta. Sem JSON válido, devolve o texto como veio e nenhuma referência (comportamento anterior).
    /// </summary>
    public static (string Text, List<DeclaredReference> References) Parse(string raw)
    {
        string body = (raw ?? "").Trim();
        int open = body.IndexOf('{');
        int close = body.LastIndexOf('}');
        if (open < 0 || close <= open)
        {
            return (body, []);
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(body[open..(close + 1)]) as JsonObject;
        }
        catch (JsonException)
        {
            return (body, []);
        }

        string? answer = root?["answer"] is JsonValue a && a.TryGetValue(out string? s) ? s?.Trim() : null;
        if (string.IsNullOrEmpty(answer))
        {
            return (body, []);
        }

        var refs = new List<DeclaredReference>();
        var seen = new HashSet<int>();
        if (root!["references"] is JsonArray rows)
        {
            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject r || r["n"] is not JsonValue nv || !nv.TryGetValue(out int n) || n < 1 || !seen.Add(n))
                {
                    continue;
                }

                string? identifier = Str(r, "identifier")?.Trim();
                if (string.IsNullOrEmpty(identifier) || identifier.Length > MaxIdentifierLength || identifier.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    identifier = null;
                }

                string label = Clip(Str(r, "label")?.Trim() ?? "", MaxLabelLength);
                if (label.Length == 0)
                {
                    label = identifier ?? "";
                }

                if (label.Length == 0)
                {
                    continue;
                }

                refs.Add(new DeclaredReference(n, identifier, Clip(label, MaxLabelLength)));
                if (refs.Count == MaxReferences)
                {
                    break;
                }
            }
        }

        return (answer, refs);
    }

    /// <summary>
    /// Troca cada [n] pela posição final da fonte (1, 2, …). Marcadores sem fonte correspondente são removidos.
    /// </summary>
    public static string Renumber(string text, IReadOnlyDictionary<int, int> finalPosition)
    {
        string replaced = Marker().Replace(text, m =>
            int.TryParse(m.Groups[1].Value, out int n) && finalPosition.TryGetValue(n, out int pos) ? $"[{pos}]" : "");
        return ExtraSpace().Replace(replaced, "$1").Trim();
    }

    private static string? Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"\[(\d{1,3})\]")]
    private static partial Regex Marker();

    // Espaço que sobra antes de pontuação quando um marcador é removido ("frase [9]." → "frase.").
    [GeneratedRegex(@"[ \t]+([.,;:!?])")]
    private static partial Regex ExtraSpace();
}
