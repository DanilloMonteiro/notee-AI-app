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
        string text = note.PlainText ?? "";
        var category = categories.Select(c => new { c.Id, c.Name, Score = Score(note.Title.Value, text, c.Name) })
            .Where(c => c.Score > 0).OrderByDescending(c => c.Score).FirstOrDefault();
        var existingTags = tags.Select(t => new { t.Id, t.Name, Score = Score(note.Title.Value, text, t.Name) })
            .Where(t => t.Score > 0).OrderByDescending(t => t.Score)
            .Select(t => new TaxonomyCandidate(t.Name, t.Id, false, "Nome encontrado na nota", t.Score)).ToList();
        var words = Regex.Matches(Fold(note.Title.Value + " " + text), "[a-z]{5,}").Select(m => m.Value)
            .Where(w => !new[] { "sobre", "entre", "para", "muito", "notas", "dados", "texto", "pelas", "pelos" }.Contains(w));
        var known = tags.Select(t => Fold(t.Name)).ToHashSet();
        var fresh = words.GroupBy(w => w).Where(g => g.Count() >= 2 && !known.Contains(g.Key))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new TaxonomyCandidate(g.Key, null, true, "Termo recorrente na nota", g.Count()));
        var allTags = existingTags.Concat(fresh).Take(5).ToList();
        TaxonomyCandidate? chosen = category is null ? null : new(category.Name, category.Id, false, "Nome encontrado na nota", category.Score);
        if (chosen is null && allTags.Count > 0) chosen = new(allTags[0].Name, null, true, "Nenhuma categoria existente combina", 1);
        return new(chosen, allTags);
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

    private static int Score(string title, string text, string name) =>
        (Regex.IsMatch(Fold(title), $@"\b{Regex.Escape(Fold(name))}\b") ? 5 : 0) +
        (Regex.IsMatch(Fold(text), $@"\b{Regex.Escape(Fold(name))}\b") ? 1 : 0);

    private static string Fold(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
