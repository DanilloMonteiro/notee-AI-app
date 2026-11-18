using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

/// <summary>
/// Busca na web do DeepSeek (ADR-046): <c>POST {BaseUrl}/v1/tools/search</c>, mesma chave do chat.
/// Envia só a consulta (pergunta ou título/descrição) — nunca <c>localization</c> (IP do usuário).
/// Cobrança do DeepSeek: só quando devolve resultados; falha, vazio e tempo esgotado não são cobrados.
/// </summary>
public sealed class DeepSeekWebSearch(HttpClient http, AiSettings settings) : ISourceSearchProvider
{
    public const string ProviderName = "deepseek-search";
    public const int RequestedCount = 10;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly AiProviderInfo Provider = AiProviders.Find(AiProviders.DeepSeek)!;

    public string Name => ProviderName;

    public SourceOrigin Origin => SourceOrigin.Web;

    public bool Available => settings.IsConfigured(Provider);

    /// <summary>Busca com as palavras-chave em português (o buscador já cobre pt e en) e a pergunta como intenção.</summary>
    public Task<IReadOnlyList<SourceHit>> SearchAsync(SourceQuery query, CancellationToken ct)
    {
        (string q, string intent) = WebSearchQuery.Build(query.Pt);
        return SearchAsync(q, string.IsNullOrWhiteSpace(query.Intent) ? intent : query.Intent, ct);
    }

    public async Task<IReadOnlyList<SourceHit>> SearchAsync(string query, string? intent, CancellationToken ct)
    {
        string apiKey = settings.ApiKeyFor(Provider) is { } key && AiProviders.IsRealKey(key)
            ? key
            : throw new SourceSearchException("Busca na web sem chave configurada.");

        var body = new
        {
            query,
            count = RequestedCount,
            ori_question = string.IsNullOrWhiteSpace(intent) ? null : intent,
            need_content = false,
        };

        // Uma nova tentativa para erros temporários (408, 429, 5xx), como a documentação recomenda.
        for (int attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrlFor(Provider)}/v1/tools/search")
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new SourceSearchException("A busca na web demorou demais.");
            }
            catch (HttpRequestException)
            {
                throw new SourceSearchException("Busca na web indisponível.");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return Parse(JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)));
                }

                int status = (int)response.StatusCode;
                bool transient = status is 408 or 429 || status >= 500;
                if (transient && attempt == 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(600), ct);
                    continue;
                }

                // O corpo do erro não é propagado (pode ecoar a consulta).
                throw new SourceSearchException(status switch
                {
                    (int)HttpStatusCode.Unauthorized => "Chave do DeepSeek inválida para a busca.",
                    (int)HttpStatusCode.PaymentRequired => "Sem saldo no DeepSeek para a busca na web.",
                    429 => "Limite de buscas do DeepSeek atingido.",
                    _ => $"Falha na busca na web ({status}).",
                }, status);
            }
        }
    }

    /// <summary>Lê <c>results</c>; ignora itens sem título ou sem URL http(s).</summary>
    public static IReadOnlyList<SourceHit> Parse(JsonNode? json)
    {
        var list = new List<SourceHit>();
        foreach (JsonNode? node in json?["results"]?.AsArray() ?? [])
        {
            if (node is not JsonObject r)
            {
                continue;
            }

            string? url = Str(r, "url");
            string title = Str(r, "title")?.Trim() ?? "";
            if (title.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            DateTimeOffset? date = DateTimeOffset.TryParse(Str(r, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset d)
                ? d
                : null;
            string summary = Str(r, "summary")?.Trim() ?? "";
            list.Add(new SourceHit(
                title,
                uri.ToString(),
                summary.Length > 0 ? summary : Str(r, "snippet")?.Trim() ?? "",
                Str(r, "site_name")?.Trim() is { Length: > 0 } site ? site : null,
                date,
                [],
                null,
                SourceOrigin.Web,
                ProviderName,
                Math.Clamp((Num(r, "rerank_score") ?? 0) / 3.0, 0, 1),
                Num(r, "authority") is { } a ? (int)a : -1));
        }

        return list;
    }

    private static string? Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static double? Num(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out double n) ? n : null;
}
