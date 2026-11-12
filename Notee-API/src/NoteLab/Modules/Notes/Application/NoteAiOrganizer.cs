using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NoteLab.Application;

/// <summary>Bloco devolvido pelo organizador por IA (ADR-037). Gráficos existentes voltam só como referência.</summary>
public sealed record OrganizedBlock(string Type, string? Text = null, int? Level = null, int? Ref = null);

public sealed record OrganizedNote(string? Title, IReadOnlyList<OrganizedBlock> Blocks);

/// <summary>Falha de validação da resposta da IA: nada é aplicado.</summary>
public sealed class OrganizeRejectedException(string message) : Exception(message);

/// <summary>
/// Regras do "Organizar com IA" (ADR-037): prompt fixo, interpretação da resposta e verificação de que a IA
/// apenas reestruturou o texto — sem omitir nem inventar conteúdo. A nota nunca é alterada no servidor.
/// </summary>
public static partial class NoteAiOrganizer
{
    public const int MaxInputChars = 50_000;
    public const int MaxOutputTokens = 16_000;
    public const double MaxMissingRatio = 0.02;
    public const double MaxAddedRatio = 0.01;

    public const string OpenMarker = "<<<NOTA";
    public const string CloseMarker = "NOTA>>>";

    private static readonly string[] AllowedTypes = ["heading", "paragraph", "list", "quote", "code", "table", "chart"];

    public const string SystemPrompt =
        "Você organiza notas em tópicos para leitura fácil. NÃO reescreva, NÃO resuma, NÃO traduza, NÃO acrescente " +
        "nem remova palavras: apenas reorganize a estrutura. Responda SOMENTE com JSON no formato " +
        "{\"title\": string|null, \"blocks\": [{\"type\": \"heading\"|\"paragraph\"|\"list\"|\"quote\"|\"code\"|\"table\"|\"chart\", " +
        "\"level\": 1|2|3 (só para heading), \"text\": string, \"ref\": número (só para chart)}]}. " +
        "Regras: títulos de seção viram heading (1 = principal, 2 = seção, 3 = subseção); itens de lista ficam um por linha " +
        "em \"text\" de um bloco list, sem marcadores; remova marcação markdown (#, **, ---); tabelas ficam inteiras e literais " +
        "num bloco table (linhas originais, incluindo cabeçalho); cada linha [[grafico:N]] vira {\"type\":\"chart\",\"ref\":N} " +
        "na mesma posição; \"title\" só se a nota tiver um título principal claro, usando as palavras dele.";

    public static string UserPrompt(string text) => $"{OpenMarker}\n{text}\n{CloseMarker}";

    /// <summary>Texto entre os marcadores do prompt (usado pelo provedor simulado).</summary>
    public static string? ExtractNote(string userPrompt)
    {
        int start = userPrompt.IndexOf(OpenMarker + "\n", StringComparison.Ordinal);
        int end = userPrompt.LastIndexOf("\n" + CloseMarker, StringComparison.Ordinal);
        return start < 0 || end < start ? null : userPrompt[(start + OpenMarker.Length + 1)..end];
    }

    /// <summary>Estimativa grosseira (≈ 4 caracteres por token) usada antes do envio.</summary>
    public static int EstimateTokens(string text) => (int)Math.Ceiling((SystemPrompt.Length + text.Length + 40) / 4.0);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Interpreta a resposta (tolerando cercas ```json) e valida tipos e campos.</summary>
    public static OrganizedNote Parse(string aiText)
    {
        string body = aiText.Trim();
        int open = body.IndexOf('{');
        int close = body.LastIndexOf('}');
        if (open < 0 || close <= open)
        {
            throw new OrganizeRejectedException("A IA não devolveu JSON válido; nada foi aplicado.");
        }

        OrganizedNote? note;
        try
        {
            note = JsonSerializer.Deserialize<OrganizedNote>(body[open..(close + 1)], Json);
        }
        catch (JsonException)
        {
            throw new OrganizeRejectedException("A IA não devolveu JSON válido; nada foi aplicado.");
        }

        if (note?.Blocks is null || note.Blocks.Count == 0)
        {
            throw new OrganizeRejectedException("A IA não devolveu blocos; nada foi aplicado.");
        }

        var clean = new List<OrganizedBlock>();
        foreach (OrganizedBlock b in note.Blocks)
        {
            string type = (b.Type ?? "").Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(type))
            {
                throw new OrganizeRejectedException($"Tipo de bloco inesperado: {b.Type}; nada foi aplicado.");
            }

            if (type == "chart")
            {
                if (b.Ref is null)
                {
                    throw new OrganizeRejectedException("Referência de gráfico ausente; nada foi aplicado.");
                }

                clean.Add(new OrganizedBlock("chart", Ref: b.Ref));
                continue;
            }

            string text = b.Text ?? "";
            if (text.Trim().Length == 0)
            {
                continue;
            }

            int? level = type == "heading" ? Math.Clamp(b.Level ?? 2, 1, 3) : null;
            clean.Add(new OrganizedBlock(type, text, level));
        }

        string? title = string.IsNullOrWhiteSpace(note.Title) ? null : note.Title.Trim();
        return new OrganizedNote(title is { Length: > 100 } ? title[..100] : title, clean);
    }

    /// <summary>
    /// Garante que a IA só reestruturou: as mesmas palavras (tolerância pequena para marcação) e cada gráfico uma vez.
    /// </summary>
    public static void Validate(string input, OrganizedNote result)
    {
        var expectedRefs = ChartMarker().Matches(input).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        var refs = result.Blocks.Where(b => b.Type == "chart").Select(b => b.Ref!.Value).ToList();
        if (refs.Count != expectedRefs.Count || refs.Distinct().Count() != refs.Count || !refs.ToHashSet().SetEquals(expectedRefs))
        {
            throw new OrganizeRejectedException("A IA perdeu ou duplicou gráficos; nada foi aplicado.");
        }

        Dictionary<string, int> before = Count(Words(input));
        Dictionary<string, int> after = Count(Words(Reconstruct(result)));
        int total = Math.Max(1, before.Values.Sum());
        int missing = before.Sum(kv => Math.Max(0, kv.Value - after.GetValueOrDefault(kv.Key)));
        int added = after.Sum(kv => Math.Max(0, kv.Value - before.GetValueOrDefault(kv.Key)));

        if (missing > total * MaxMissingRatio)
        {
            throw new OrganizeRejectedException("A IA omitiu parte do conteúdo; nada foi aplicado.");
        }

        if (added > total * MaxAddedRatio)
        {
            throw new OrganizeRejectedException("A IA acrescentou conteúdo que não estava na nota; nada foi aplicado.");
        }
    }

    public static string Reconstruct(OrganizedNote note)
    {
        var sb = new StringBuilder();
        if (note.Title is not null)
        {
            sb.AppendLine(note.Title);
        }

        foreach (OrganizedBlock b in note.Blocks)
        {
            sb.AppendLine(b.Type == "chart" ? $"[[grafico:{b.Ref}]]" : b.Text);
        }

        return sb.ToString();
    }

    public static IEnumerable<string> Words(string text) =>
        Word().Matches(text.Normalize(NormalizationForm.FormC).ToLowerInvariant()).Select(m => m.Value);

    private static Dictionary<string, int> Count(IEnumerable<string> words) =>
        words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Organizador do provedor simulado (sem custo, determinístico): divide por linhas em branco,
    /// "#" vira título, "- " vira lista, "|" vira tabela, [[grafico:N]] vira referência.
    /// </summary>
    public static string FakeOrganize(string text)
    {
        var blocks = new List<OrganizedBlock>();
        foreach (string chunk in Regex.Split(text.Replace("\r\n", "\n"), @"\n\s*\n"))
        {
            string part = chunk.Trim();
            if (part.Length == 0 || Regex.IsMatch(part, @"^-{3,}$"))
            {
                continue;
            }

            Match chart = ChartMarker().Match(part);
            if (chart.Success && chart.Value == part)
            {
                blocks.Add(new OrganizedBlock("chart", Ref: int.Parse(chart.Groups[1].Value, CultureInfo.InvariantCulture)));
                continue;
            }

            Match heading = Regex.Match(part, @"^(#{1,6})\s+(.+)$");
            if (heading.Success && !part.Contains('\n'))
            {
                blocks.Add(new OrganizedBlock("heading", heading.Groups[2].Value.Trim(), Math.Min(heading.Groups[1].Value.Length, 3)));
                continue;
            }

            string[] lines = part.Split('\n');
            if (lines.All(l => l.TrimStart().StartsWith('|')))
            {
                blocks.Add(new OrganizedBlock("table", part));
            }
            else if (lines.All(l => Regex.IsMatch(l, @"^\s*[-*+]\s+")))
            {
                blocks.Add(new OrganizedBlock("list", string.Join("\n", lines.Select(l => Regex.Replace(l, @"^\s*[-*+]\s+", "")))));
            }
            else
            {
                blocks.Add(new OrganizedBlock("paragraph", part.Replace("**", "")));
            }
        }

        return JsonSerializer.Serialize(new OrganizedNote(null, blocks), Json);
    }

    [GeneratedRegex(@"\[\[grafico:(\d+)\]\]")]
    private static partial Regex ChartMarker();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}

/// <summary>Preço opcional por provedor (USD por milhão de tokens). Sem configuração, o custo não é informado.</summary>
public static class AiPricing
{
    public static decimal? Cost(IConfiguration config, string providerId, int? inputTokens, int? outputTokens)
    {
        decimal? inPrice = config.GetValue<decimal?>($"Ai:Pricing:{providerId}:InputPerMillionUsd");
        decimal? outPrice = config.GetValue<decimal?>($"Ai:Pricing:{providerId}:OutputPerMillionUsd");
        if (inPrice is null || outPrice is null || inputTokens is null)
        {
            return null;
        }

        decimal cost = (inputTokens.Value * inPrice.Value + (outputTokens ?? 0) * outPrice.Value) / 1_000_000m;
        return Math.Round(cost, 6);
    }
}
