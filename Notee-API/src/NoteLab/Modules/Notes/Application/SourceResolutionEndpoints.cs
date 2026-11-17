using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public sealed record ResolveSourceRequest(string Input);
public sealed record ResolvedSource(string Kind, string Title, List<string> Authors, int? Year, string? Venue,
    string? Doi, string? Isbn, string? Url, string? License, string Provider, string? Message);

public static partial class SourceResolutionEndpoints
{
    public static void MapSourceResolutionEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/sources/resolve", ResolveAsync).RequireAuthorization().WithTags("Sources");

    private static async Task<IResult> ResolveAsync(ResolveSourceRequest request, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        string input = (request.Input ?? "").Trim();
        if (input.Length is < 1 or > 2048) return Results.Problem(statusCode: 400, title: "Entrada inválida", detail: "Informe DOI, ISBN ou URL (até 2048 caracteres).");
        try
        {
            ResolvedSource? resolved = await ResolveInputAsync(input, http, config, ct);
            return resolved is null
                ? Results.Problem(statusCode: 400, title: "Entrada não reconhecida", detail: "Use um DOI, ISBN ou URL http(s) válida.")
                : Results.Ok(resolved);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is SafeHttpException or HttpRequestException or JsonException or InvalidOperationException or FormatException)
        {
            return Results.Problem(statusCode: 502, title: "Não foi possível consultar a fonte", detail: $"A consulta ao provedor falhou ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    /// Confere se um DOI/ISBN/URL sugerido existe (ADR-044). Devolve null quando a entrada não é reconhecida,
    /// a consulta falha, a página responde erro ou não há título — nesses casos a sugestão é descartada.
    /// </summary>
    public static async Task<ResolvedSource?> TryResolveAsync(string? input, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        string value = (input ?? "").Trim();
        if (value.Length is < 1 or > 2048) return null;
        try
        {
            ResolvedSource? resolved = await ResolveInputAsync(value, http, config, ct);
            return resolved is { Message: null } && !string.IsNullOrWhiteSpace(resolved.Title) ? resolved : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return null; // qualquer falha de consulta = fonte não conferida
        }
    }

    /// <summary>Resolve DOI, ISBN, YouTube ou URL. Null quando a entrada não é reconhecida.</summary>
    private static async Task<ResolvedSource?> ResolveInputAsync(string input, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        try
        {
            if (Source.NormalizeDoi(input) is { } doi)
                return await ResolveDoiAsync(doi, http, config, ct);
        }
        catch (DomainException) { }
        try
        {
            string clean = Regex.Replace(input, @"[\s-]", "");
            if ((clean.Length is 10 or 13) && Source.NormalizeIsbn(input) is { } isbn)
                return await ResolveIsbnAsync(isbn, http, config, ct);
        }
        catch (DomainException) { }
        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return null;
        if (uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
            return await ResolveYoutubeAsync(uri, http, config, ct);
        SafeHttpResponse page = await http.GetAsync(uri.ToString(), ct);
        if ((int)page.StatusCode >= 400) return new ResolvedSource("website", "", [], null, null, null, null, uri.ToString(), null, "url", $"A página respondeu HTTP {(int)page.StatusCode}.");
        string html = page.ReadText();
        string title = SourcePageMetadata.PageTitle(html) is { Length: > 0 } pageTitle ? pageTitle : page.FinalUri.Host;
        int? year = SourcePageMetadata.PublicationYear(html);
        List<string> authors = SourcePageMetadata.Authors(html);
        string? license = SourcePageMetadata.CreativeCommonsLicense(html);
        return new ResolvedSource("website", Decode(title), authors.Select(Decode).ToList(), year,
            Meta(html, "citation_journal_title") ?? Meta(html, "og:site_name"), Meta(html, "citation_doi"),
            null, page.FinalUri.ToString(), license, "url", null);
    }

    private static async Task<ResolvedSource> ResolveDoiAsync(string doi, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        string baseUrl = Provider(config, "CrossrefBaseUrl", "https://api.crossref.org");
        string contact = config["Sources:ContactEmail"] ?? "";
        string suffix = $"/works/{Uri.EscapeDataString(doi)}" + (string.IsNullOrWhiteSpace(contact) ? "" : $"?mailto={Uri.EscapeDataString(contact)}");
        SafeHttpResponse response = await http.GetAsync(baseUrl.TrimEnd('/') + suffix, ct, allowJson: true);
        using JsonDocument json = JsonDocument.Parse(response.Body);
        JsonElement message = json.RootElement.GetProperty("message");
        string title = First(message, "title") ?? doi;
        var authors = message.TryGetProperty("author", out JsonElement authorRows) && authorRows.ValueKind == JsonValueKind.Array
            ? authorRows.EnumerateArray().Select(a => string.Join(" ", new[] { GetString(a, "given"), GetString(a, "family") }.Where(x => !string.IsNullOrWhiteSpace(x)))).Where(a => a.Length > 0).Take(20).ToList() : [];
        int? year = null;
        foreach (string dateField in new[] { "published-print", "published-online", "created" })
            if (message.TryGetProperty(dateField, out JsonElement date) && date.TryGetProperty("date-parts", out JsonElement parts) && parts[0][0].TryGetInt32(out int found)) { year = found; break; }
        return new("article", title, authors, year, First(message, "container-title"), doi, null,
            $"https://doi.org/{doi}", null, "crossref", null);
    }

    private static async Task<ResolvedSource> ResolveIsbnAsync(string isbn, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        string baseUrl = Provider(config, "OpenLibraryBaseUrl", "https://openlibrary.org");
        SafeHttpResponse response = await http.GetAsync($"{baseUrl.TrimEnd('/')}/isbn/{isbn}.json", ct, allowJson: true);
        using JsonDocument json = JsonDocument.Parse(response.Body);
        JsonElement root = json.RootElement;
        string title = GetString(root, "title") ?? "";
        var authors = new List<string>();
        if (root.TryGetProperty("authors", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
            authors = rows.EnumerateArray().Select(a => GetString(a, "name") ?? GetString(a, "key")?.Split('/').Last()).Where(a => !string.IsNullOrWhiteSpace(a)).Cast<string>().Take(20).ToList();
        int? year = int.TryParse(GetString(root, "publish_date") is { } date ? Regex.Match(date, @"\b(?:19|20)\d{2}\b").Value : "", out int y) ? y : null;
        string? publisher = root.TryGetProperty("publishers", out JsonElement pubs) && pubs.ValueKind == JsonValueKind.Array ? pubs[0].GetString() : null;
        return new("book", title, authors, year, publisher, null, isbn, null, null, "openlibrary", null);
    }

    private static async Task<ResolvedSource> ResolveYoutubeAsync(Uri video, SafeHttpClient http, IConfiguration config, CancellationToken ct)
    {
        string baseUrl = Provider(config, "OEmbedBaseUrl", "https://www.youtube.com/oembed");
        string endpoint = baseUrl.Contains('?') ? baseUrl + "&" : baseUrl + "?";
        SafeHttpResponse response = await http.GetAsync(endpoint + "url=" + Uri.EscapeDataString(video.ToString()) + "&format=json", ct, allowJson: true);
        using JsonDocument json = JsonDocument.Parse(response.Body);
        JsonElement root = json.RootElement;
        return new("video", GetString(root, "title") ?? video.ToString(),
            string.IsNullOrWhiteSpace(GetString(root, "author_name")) ? [] : [GetString(root, "author_name")!],
            null, GetString(root, "author_name"), null, null, video.ToString(), null, "youtube-oembed", null);
    }

    private static string Provider(IConfiguration config, string name, string fallback) => config[$"Sources:Providers:{name}"] ?? fallback;
    private static string? First(JsonElement root, string property) => root.TryGetProperty(property, out JsonElement v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0 ? v[0].GetString() : null;
    private static string? GetString(JsonElement root, string property) => root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Meta(string html, string name) => MetaRegex(name).Match(html) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    private static string Decode(string value) => WebUtility.HtmlDecode(value).Trim();
    private static Regex MetaRegex(string name) => new($"<meta\\b(?=[^>]*(?:name|property)\\s*=\\s*['\\\"]{Regex.Escape(name)}['\\\"])[^>]*\\bcontent\\s*=\\s*['\\\"]([^'\\\"]*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
}
