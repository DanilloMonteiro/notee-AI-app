using System.Text.Json;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record AiSourceCheckResult(string Verdict, List<AiSourceCheckArgument> Arguments);
public sealed record AiSourceCheckArgument(string Point, string SourceExcerpt);

public static class SourceMetadataComparer
{
    public static List<SourceCheckArgument> Compare(string enteredTitle, int? enteredYear, string? foundTitle, int? foundYear, string evidenceUrl,
        IEnumerable<string>? enteredAuthors = null, IEnumerable<string>? foundAuthors = null)
    {
        var args = new List<SourceCheckArgument>();
        if (foundYear.HasValue && enteredYear.HasValue && foundYear != enteredYear)
            args.Add(new("Ano divergente", $"O registro lista {foundYear}; a biblioteca informa {enteredYear}.", evidenceUrl));
        if (!string.IsNullOrWhiteSpace(foundTitle) && !string.Equals(Normalize(foundTitle), Normalize(enteredTitle), StringComparison.Ordinal))
            args.Add(new("Título divergente", $"O registro lista “{foundTitle}”; a biblioteca informa “{enteredTitle}”.", evidenceUrl));
        string? expectedAuthor = enteredAuthors?.FirstOrDefault();
        string? actualAuthor = foundAuthors?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(expectedAuthor) && !string.IsNullOrWhiteSpace(actualAuthor) &&
            !string.Equals(Normalize(expectedAuthor), Normalize(actualAuthor), StringComparison.Ordinal))
            args.Add(new("Autor divergente", $"O registro lista “{actualAuthor}” como primeiro autor; a biblioteca informa “{expectedAuthor}”.", evidenceUrl));
        return args;
    }

    public static string Normalize(string value)
    {
        string decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        char[] chars = decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray();
        return string.Join(' ', new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public static class SourceQuoteMatcher
{
    public static SourceCheckResult Compare(IReadOnlyList<ExtractedPage> pages, string quote, int? requestedStart = null, int? requestedEnd = null, string? evidenceUrl = null)
    {
        if (pages.Count == 0) return new(SourceCheckStatus.Unverifiable, "O arquivo não contém texto extraível.", Array.Empty<SourceCheckArgument>(), "archive");
        if (string.IsNullOrWhiteSpace(quote)) return new(SourceCheckStatus.Unverifiable, "Informe o trecho da fonte para fazer a conferência.", Array.Empty<SourceCheckArgument>(), "archive");
        string expected = SourceMetadataComparer.Normalize(quote);
        List<ExtractedPage> requested = requestedStart.HasValue
            ? pages.Where(p => p.Page >= requestedStart && p.Page <= (requestedEnd ?? requestedStart)).ToList()
            : pages.ToList();
        ExtractedPage? exact = requested.FirstOrDefault(p => SourceMetadataComparer.Normalize(p.Text).Contains(expected, StringComparison.Ordinal));
        if (exact is not null) return new(SourceCheckStatus.Confirmed, $"Trecho encontrado literalmente na página {exact.Page}.",
            [new("Trecho localizado", quote, evidenceUrl)], "archive");
        ExtractedPage? elsewhere = pages.FirstOrDefault(p => SourceMetadataComparer.Normalize(p.Text).Contains(expected, StringComparison.Ordinal));
        if (elsewhere is not null) return new(SourceCheckStatus.FoundWithDifferences,
            $"O trecho foi encontrado na página {elsewhere.Page}, fora da localização informada.",
            [new("Localização divergente", $"Esperada p. {requestedStart}; encontrada p. {elsewhere.Page}.", evidenceUrl)], "archive");
        var closest = pages.Select(p => (Page: p, Score: Similarity(expected, SourceMetadataComparer.Normalize(p.Text)))).OrderByDescending(x => x.Score).First();
        if (closest.Score >= 0.75) return new(SourceCheckStatus.FoundWithDifferences, $"Foi localizado um trecho semelhante na página {closest.Page.Page}.",
            [new("Possível diferença", $"Trecho informado: “{quote}”. Confira a página {closest.Page.Page} no arquivo.", evidenceUrl)], "archive");
        return new(SourceCheckStatus.QuoteNotFound, "O trecho não foi localizado no texto arquivado.", [new("Trecho procurado", quote, evidenceUrl)], "archive");
    }

    private static double Similarity(string expected, string actual)
    {
        string[] words = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
        return words.Length == 0 ? 0 : (double)words.Count(word => actual.Contains(word, StringComparison.Ordinal)) / words.Length;
    }
}

public sealed class InvalidAiSourceCheckException(string message) : Exception(message);

public static class SourceAiResponseValidator
{
    public static AiSourceCheckResult Parse(string raw, string excerpt)
    {
        int start = raw.IndexOf('{'); int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidAiSourceCheckException("A resposta não contém JSON válido.");
        AiSourceCheckResult? result;
        try { result = JsonSerializer.Deserialize<AiSourceCheckResult>(raw[start..(end + 1)], new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { throw new InvalidAiSourceCheckException("O JSON da resposta é inválido."); }
        if (result is null || result.Arguments is null || result.Verdict is not ("supports" or "partially" or "contradicts" or "insufficient") ||
            result.Arguments.Count > 8 || result.Arguments.Any(a => string.IsNullOrWhiteSpace(a.Point) || a.Point.Length > 500 ||
                string.IsNullOrWhiteSpace(a.SourceExcerpt) || !excerpt.Contains(a.SourceExcerpt, StringComparison.Ordinal)))
            throw new InvalidAiSourceCheckException("A resposta não respeitou o formato ou citou evidência ausente do trecho enviado.");
        return result;
    }
}
