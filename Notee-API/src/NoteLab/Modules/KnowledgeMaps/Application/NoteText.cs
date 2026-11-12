using System.Text.Json;

namespace NoteLab.Application;

/// <summary>
/// Extrai texto simples do conteúdo da nota. O editor grava um array JSON de blocos
/// <c>[{ id, type, text }]</c>; notas antigas podem conter texto puro.
/// </summary>
public static class NoteText
{
    public static string Extract(string? content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string text = content;
        string trimmed = content.TrimStart();

        if (trimmed.StartsWith('['))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    IEnumerable<string> parts = doc.RootElement.EnumerateArray()
                        .Where(block => block.ValueKind == JsonValueKind.Object)
                        .Select(BlockText)
                        .Where(part => part.Length > 0);

                    text = string.Join("\n", parts);
                }
            }
            catch (JsonException)
            {
                // Não é JSON válido: trata como texto puro.
            }
        }

        text = text.Trim();
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    private static string BlockText(JsonElement block)
    {
        if (block.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String
            && type.GetString() == "chart")
        {
            return ChartText(block);
        }

        return block.TryGetProperty("text", out JsonElement t) && t.ValueKind == JsonValueKind.String
            ? t.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>Texto pesquisável de um bloco de gráfico (ADR-036): metadados, rótulos, séries e fontes; sem valores.</summary>
    private static string ChartText(JsonElement chart)
    {
        List<string> parts = [];
        foreach (string property in new[] { "title", "description", "xAxisLabel", "yAxisLabel", "unit" })
        {
            AddString(parts, chart, property);
        }

        AddStrings(parts, chart, "labels", null);
        AddStrings(parts, chart, "series", "name");
        AddStrings(parts, chart, "sources", "label");

        return string.Join(" ", parts);
    }

    private static void AddString(List<string> parts, JsonElement owner, string property)
    {
        if (owner.ValueKind == JsonValueKind.Object
            && owner.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            parts.Add(value.GetString()!.Trim());
        }
    }

    private static void AddStrings(List<string> parts, JsonElement owner, string arrayProperty, string? itemProperty)
    {
        if (!owner.TryGetProperty(arrayProperty, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (itemProperty is null)
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    parts.Add(item.GetString()!.Trim());
                }
            }
            else
            {
                AddString(parts, item, itemProperty);
            }
        }
    }
}
