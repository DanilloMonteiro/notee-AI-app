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
                        .Where(block => block.ValueKind == JsonValueKind.Object
                                        && block.TryGetProperty("text", out JsonElement t)
                                        && t.ValueKind == JsonValueKind.String)
                        .Select(block => block.GetProperty("text").GetString() ?? string.Empty)
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
}
