using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record TaxonomyCandidate(string Name, Guid? ExistingId, bool IsNew, string Reason, int Score = 0);
public sealed record TaxonomyPreview(TaxonomyCandidate? Category, IReadOnlyList<TaxonomyCandidate> Tags);

public static class TaxonomyAi
{
    private static readonly HashSet<string> StopWords = "sobre entre para pelos pelas desta deste esses essas muito ainda quando onde como mais menos todos todas texto nota notas dados uma uns nas nos das dos que por com sem sao nao foi era ser ter seu sua seus suas the and from".Split(' ').ToHashSet(StringComparer.Ordinal);

    public const string SystemPrompt = "Sugira uma categoria e até cinco tags para organizar a nota. Use nomes existentes se combinarem. Responda somente JSON: {\"category\":{\"name\":\"...\",\"reason\":\"...\"},\"tags\":[{\"name\":\"...\",\"reason\":\"...\"}]}. Não inclua texto fora do JSON.";

    public static string UserPrompt(Note note, IEnumerable<Category> categories, IEnumerable<Tag> tags) =>
        JsonSerializer.Serialize(new { title = note.Title.Value, text = (note.PlainText ?? "")[..Math.Min(12_000, (note.PlainText ?? "").Length)], categories = categories.Select(c => c.Name), tags = tags.Select(t => t.Name) });

    public static TaxonomyPreview Parse(string json, IEnumerable<Category> categories, IEnumerable<Tag> tags)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        TaxonomyCandidate? category = null;
        if (root.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.Object)
            category = Candidate(c, categories.Select(x => (x.Id, x.Name)));
        var result = new List<TaxonomyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("tags", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var candidate = Candidate(item, tags.Select(x => (x.Id, x.Name)));
                if (candidate is not null && seen.Add(Fold(candidate.Name))) result.Add(candidate);
                if (result.Count == 5) break;
            }
        return new(category, result);
    }

    public static TaxonomyPreview Fake(Note note, IEnumerable<Category> categories, IEnumerable<Tag> tags)
    {
        var headings = new List<string>();
        if (!string.IsNullOrWhiteSpace(note.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(note.Content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var block in doc.RootElement.EnumerateArray())
                        if (block.ValueKind == JsonValueKind.Object && block.TryGetProperty("type", out var type) && type.GetString() == "heading" &&
                            block.TryGetProperty("text", out var heading) && heading.ValueKind == JsonValueKind.String)
                            headings.Add(heading.GetString() ?? "");
            }
            catch (JsonException) { }
        }

        string headingText = string.Join(' ', headings);
        string body = note.PlainText ?? "";
        string taxonomyText = $"{note.Title.Value} {headingText} {body}";
        int Score(string name) => (Contains(note.Title.Value, name) ? 5 : 0) + (Contains(headingText, name) ? 3 : 0) + (Contains(body, name) ? 1 : 0);

        var categoryMatches = categories.Select(c => new { c.Id, c.Name, Score = Score(c.Name) })
            .Where(c => c.Score > 0).OrderByDescending(c => c.Score).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        TaxonomyCandidate? category = note.CategoryId.HasValue ? null : categoryMatches.Count > 0
            ? new(categoryMatches[0].Name, categoryMatches[0].Id, false, Reason(categoryMatches[0].Name, note.Title.Value, headingText, body), categoryMatches[0].Score)
            : null;

        var alreadyApplied = note.Tags.Select(t => t.Id).ToHashSet();
        var existingTags = tags.Select(t => new { t.Id, t.Name, Score = Score(t.Name) })
            .Where(t => t.Score > 0 && !alreadyApplied.Contains(t.Id)).OrderByDescending(t => t.Score).ThenBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new TaxonomyCandidate(t.Name, t.Id, false, Reason(t.Name, note.Title.Value, headingText, body), t.Score)).ToList();

        var tokens = Words(taxonomyText).Where(w => !StopWords.Contains(w)).ToList();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Count; i++)
        {
            counts[tokens[i]] = counts.GetValueOrDefault(tokens[i]) + 1;
            if (i + 1 < tokens.Count)
            {
                string pair = $"{tokens[i]} {tokens[i + 1]}";
                counts[pair] = counts.GetValueOrDefault(pair) + 1;
            }
        }
        var known = tags.Select(t => Fold(t.Name)).ToHashSet(StringComparer.Ordinal);
        var freshTags = counts.Where(x => x.Value >= 2 && !known.Contains(x.Key))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new TaxonomyCandidate(x.Key, null, true, $"Termo recorrente ({x.Value} ocorrências)", x.Value));
        var suggestedTags = existingTags.Concat(freshTags).Take(5).ToList();

        if (category is null && !note.CategoryId.HasValue)
        {
            var dominant = Words(headingText).Where(w => !StopWords.Contains(w)).GroupBy(w => w, StringComparer.Ordinal)
                .Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).FirstOrDefault();
            if (dominant is not null) category = new(dominant.Name, null, true, "Nenhuma categoria existente combina", dominant.Count);
        }
        return new(category, suggestedTags);
    }

    private static TaxonomyCandidate? Candidate(JsonElement item, IEnumerable<(Guid Id, string Name)> existing)
    {
        if (!item.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String) return null;
        string name = (nameValue.GetString() ?? "").Trim();
        if (name.Length is < 1 or > Category.NameMaxLength) return null;
        string reason = item.TryGetProperty("reason", out var reasonValue) && reasonValue.ValueKind == JsonValueKind.String
            ? (reasonValue.GetString() ?? "")[..Math.Min(200, (reasonValue.GetString() ?? "").Length)] : "Sugestão da IA";
        var found = existing.FirstOrDefault(x => Fold(x.Name) == Fold(name));
        return found.Id != Guid.Empty ? new(found.Name, found.Id, false, reason) : new(name, null, true, reason);
    }

    private static string Reason(string name, string title, string headings, string body) =>
        Contains(title, name) ? "Aparece no título da nota" : Contains(headings, name) ? "Aparece em título de seção" : "Aparece no texto";

    private static bool Contains(string text, string name) =>
        Regex.IsMatch(Fold(text), $@"(?<![a-z0-9]){Regex.Escape(Fold(name))}(?![a-z0-9])");

    private static IEnumerable<string> Words(string text) =>
        Regex.Matches(Fold(text), "[a-z0-9]{5,}").Select(m => m.Value);

    private static string Fold(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
