using System.Net;
using System.Text.RegularExpressions;

namespace NoteLab.Application;

public static partial class SourcePageMetadata
{
    public static string? NormalizeCopyLicense(string? value)
    {
        string license = (value ?? "").Trim().ToLowerInvariant();
        if (license == "cc0") return "cc-zero";
        return license is "public-domain" or "cc-by" or "cc-by-sa" or "cc-by-nd" or "cc-by-nc" or
            "cc-by-nc-sa" or "cc-by-nc-nd" or "cc-zero" or "cc-mark" ? license : null;
    }

    public static bool AllowsCopy(string? value) => NormalizeCopyLicense(value) is not null;

    public static string? PageTitle(string? html) => MetaContent(html, "og:title") ?? MetaContent(html, "citation_title") ??
        WebUtility.HtmlDecode(TitleRegex().Match(html ?? "") is { Success: true } title ? title.Groups[1].Value.Trim() : "");

    public static int? PublicationYear(string? html)
    {
        Match year = Regex.Match(MetaContent(html, "citation_publication_date") ?? MetaContent(html, "datePublished") ?? "", @"\b(?:19|20)\d{2}\b");
        return year.Success && int.TryParse(year.Value, out int parsed) ? parsed : null;
    }

    public static List<string> Authors(string? html)
    {
        List<string> authors = CitationAuthorRegex().Matches(html ?? "").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value).Trim())
            .Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        if (authors.Count > 0) return authors;
        string? author = MetaContent(html, "author");
        return string.IsNullOrWhiteSpace(author) ? [] : [author];
    }

    public static string? CreativeCommonsLicense(string? html)
    {
        foreach (Match tag in LinkTagRegex().Matches(html ?? ""))
        {
            string? rel = Attribute(tag.Value, "rel");
            if (rel is null || !rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Contains("license", StringComparer.OrdinalIgnoreCase)) continue;
            string? href = Attribute(tag.Value, "href");
            if (!Uri.TryCreate(WebUtility.HtmlDecode(href), UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !(uri.Host.Equals("creativecommons.org", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("www.creativecommons.org", StringComparison.OrdinalIgnoreCase))) continue;

            string[] path = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (path.Length >= 3 && path[0].Equals("licenses", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(path[2], @"^\d+(?:\.\d+)?$"))
            {
                string license = path[1].ToLowerInvariant();
                if (license is "by" or "by-sa" or "by-nd" or "by-nc" or "by-nc-sa" or "by-nc-nd")
                    return "cc-" + license;
            }
            if (path.Length >= 3 && path[0].Equals("publicdomain", StringComparison.OrdinalIgnoreCase) &&
                path[2].Equals("1.0", StringComparison.OrdinalIgnoreCase))
            {
                if (path[1].Equals("zero", StringComparison.OrdinalIgnoreCase)) return "cc-zero";
                if (path[1].Equals("mark", StringComparison.OrdinalIgnoreCase)) return "cc-mark";
            }
        }
        return null;
    }

    public static string ExtractVisibleText(string html)
    {
        string text = CommentRegex().Replace(html ?? "", " ");
        text = HiddenSectionRegex().Replace(text, " ");
        text = BlockTagRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        string[] lines = Regex.Split(text, @"\r?\n")
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0).ToArray();
        return string.Join("\n", lines);
    }

    private static string? Attribute(string tag, string name)
    {
        Match match = Regex.Match(tag, $"""(?:^|\s){Regex.Escape(name)}\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return null;
        for (int i = 1; i <= 3; i++) if (match.Groups[i].Success) return match.Groups[i].Value;
        return null;
    }

    private static string? MetaContent(string? html, string name)
    {
        Match match = Regex.Match(html ?? "", $"<meta\\b(?=[^>]*(?:name|property)\\s*=\\s*['\\\"]{Regex.Escape(name)}['\\\"])[^>]*\\bcontent\\s*=\\s*['\\\"]([^'\\\"]*)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    [GeneratedRegex("<link\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkTagRegex();
    [GeneratedRegex("<title\\b[^>]*>(.*?)</title\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
    [GeneratedRegex("<meta\\b(?=[^>]*\\bname\\s*=\\s*['\"]citation_author['\"])[^>]*\\bcontent\\s*=\\s*['\"]([^'\"]*)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CitationAuthorRegex();
    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
    [GeneratedRegex("<(script|style|noscript|svg)\\b[^>]*>.*?</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenSectionRegex();
    [GeneratedRegex("</?(?:address|article|aside|blockquote|br|dd|div|dl|dt|fieldset|figcaption|figure|footer|form|h[1-6]|header|hr|li|main|nav|ol|p|pre|section|table|tr|ul)\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockTagRegex();
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
