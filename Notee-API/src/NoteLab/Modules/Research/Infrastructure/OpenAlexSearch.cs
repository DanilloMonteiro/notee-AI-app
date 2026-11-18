using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

/// <summary>
/// Busca de artigos acadêmicos no OpenAlex (ADR-047): <c>GET {BaseUrl}/works?search=…</c>.
/// Envia só as palavras-chave; chave opcional (<c>OpenAlex:ApiKey</c>, cabeçalho Bearer) aumenta o orçamento diário.
/// Cobrança do OpenAlex: cada busca conta (≈ US$ 1 por 1.000); respostas de erro não contam.
/// </summary>
public sealed class OpenAlexSearch(HttpClient http, IConfiguration config) : ISourceSearchProvider
{
    public const string ProviderName = "openalex";
    public const int PerPage = 10;
    private const int MaxAbstractChars = 1_500;
    private const int MaxAuthors = 5;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // Artigos e revisões com resumo, em inglês ou português. Campos de nível raiz (o select não aceita aninhados).
    private const string Filter = "type:article|review,has_abstract:true,language:en|pt";
    private const string FallbackFilter = "has_abstract:true";
    private const string Fields = "id,doi,display_name,publication_date,authorships,primary_location,open_access,abstract_inverted_index,relevance_score";

    public string Name => ProviderName;

    public SourceOrigin Origin => SourceOrigin.Academic;

    public bool Available => config.GetValue<bool?>("Research:Academic:Enabled") ?? true;

    private string BaseUrl => (config["OpenAlex:BaseUrl"] is { Length: > 0 } url ? url : "https://api.openalex.org").TrimEnd('/');

    public async Task<IReadOnlyList<SourceHit>> SearchAsync(SourceQuery query, CancellationToken ct)
    {
        string terms = string.IsNullOrWhiteSpace(query.En) ? query.Pt : query.En;
        if (string.IsNullOrWhiteSpace(terms))
        {
            return [];
        }

        string filter = Filter;
        for (int attempt = 1; ; attempt++)
        {
            string url = $"{BaseUrl}/works?search={Uri.EscapeDataString(terms)}&filter={Uri.EscapeDataString(filter)}" +
                         $"&per_page={PerPage}&select={Fields}";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (config["OpenAlex:ApiKey"] is { } key && AiProviders.IsRealKey(key))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            }

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new SourceSearchException("A busca de artigos demorou demais.");
            }
            catch (HttpRequestException)
            {
                throw new SourceSearchException("Busca de artigos indisponível.");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return Parse(JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)));
                }

                int status = (int)response.StatusCode;
                // Filtro recusado (400): tenta uma vez só com "tem resumo".
                if (status == 400 && filter != FallbackFilter && attempt < 3)
                {
                    filter = FallbackFilter;
                    continue;
                }

                bool transient = status == 429 || status >= 500;
                if (transient && attempt == 1)
                {
                    TimeSpan wait = response.Headers.RetryAfter?.Delta is { } delta && delta < TimeSpan.FromSeconds(2) ? delta : TimeSpan.FromMilliseconds(800);
                    await Task.Delay(wait, ct);
                    continue;
                }

                // O corpo do erro não é propagado (pode ecoar a consulta).
                throw new SourceSearchException(status switch
                {
                    401 or 403 => "Chave do OpenAlex inválida ou não ativada.",
                    429 => "Limite diário de buscas de artigos atingido.",
                    _ => $"Falha na busca de artigos ({status}).",
                }, status);
            }
        }
    }

    /// <summary>
    /// Lê <c>results</c>: link pelo DOI › cópia aberta › página do editor; resumo remontado do índice invertido;
    /// relevância normalizada pela maior da resposta (0–1). Itens sem título, link ou resumo são ignorados.
    /// </summary>
    public static IReadOnlyList<SourceHit> Parse(JsonNode? json)
    {
        var rows = new List<(SourceHit Hit, double Relevance)>();
        foreach (JsonNode? node in json?["results"]?.AsArray() ?? [])
        {
            if (node is not JsonObject w)
            {
                continue;
            }

            string title = Str(w, "display_name")?.Trim() ?? "";
            string? doiUrl = Str(w, "doi");
            string? doi = doiUrl is null ? null : ShortDoi(doiUrl);
            string? url = Http(doiUrl) ?? Http(Str(w["open_access"] as JsonObject, "oa_url"))
                          ?? Http(Str(w["primary_location"] as JsonObject, "landing_page_url"));
            string excerpt = Abstract(w["abstract_inverted_index"] as JsonObject);
            if (title.Length == 0 || url is null || excerpt.Length == 0)
            {
                continue;
            }

            DateTimeOffset? date = DateTimeOffset.TryParse(Str(w, "publication_date"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset d) ? d : null;
            string? venue = Str((w["primary_location"] as JsonObject)?["source"] as JsonObject, "display_name")?.Trim();
            List<string> authors = (w["authorships"] as JsonArray ?? [])
                .Select(a => Str((a as JsonObject)?["author"] as JsonObject, "display_name")?.Trim())
                .OfType<string>().Where(a => a.Length > 0).Take(MaxAuthors).ToList();
            double relevance = w["relevance_score"] is JsonValue rv && rv.TryGetValue(out double r) ? r : 0;
            rows.Add((new SourceHit(title, url, excerpt, venue is { Length: > 0 } ? venue : null, date, authors, doi,
                SourceOrigin.Academic, ProviderName, 0), relevance));
        }

        double max = rows.Count == 0 ? 0 : rows.Max(r => r.Relevance);
        // Sem nota de relevância (não deveria acontecer em buscas): todos ficam no meio da escala.
        return rows.Select(r => r.Hit with { Score = max > 0 ? Math.Round(r.Relevance / max, 4) : 0.5 }).ToList();
    }

    /// <summary>Remonta o resumo: cada palavra vai para as posições indicadas no índice invertido.</summary>
    public static string Abstract(JsonObject? inverted)
    {
        if (inverted is null)
        {
            return "";
        }

        var words = new SortedDictionary<int, string>();
        foreach ((string word, JsonNode? positions) in inverted)
        {
            foreach (JsonNode? p in positions as JsonArray ?? [])
            {
                if (p is JsonValue v && v.TryGetValue(out int index) && index >= 0 && index < 5_000)
                {
                    words[index] = word;
                }
            }
        }

        string text = string.Join(' ', words.Values).Trim();
        return text.Length <= MaxAbstractChars ? text : text[..MaxAbstractChars].TrimEnd() + "…";
    }

    private static string ShortDoi(string doi)
    {
        string value = doi.Trim();
        int at = value.IndexOf("doi.org/", StringComparison.OrdinalIgnoreCase);
        return at >= 0 ? value[(at + "doi.org/".Length)..] : value;
    }

    private static string? Http(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;

    private static string? Str(JsonObject? o, string key) => o?[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
