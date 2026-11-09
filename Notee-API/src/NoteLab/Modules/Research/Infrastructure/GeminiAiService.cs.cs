using System.Net.Http.Json;
using System.Text.Json.Nodes;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

/// <summary>
/// Provider Google Gemini (M13). Usado SOMENTE com consentimento da conta (ADR-019).
/// Pede busca com grounding e devolve apenas as fontes que a API retornar em groundingMetadata —
/// nunca fontes inventadas. A chave vai no cabeçalho x-goog-api-key, não na URL.
/// </summary>
public class GeminiAiService : IAiService
{
    public const int MaxSources = 10;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _configModel;
    private readonly AiSettings? _settings;
    private readonly string _baseUrl;

    public GeminiAiService(IConfiguration config, HttpClient httpClient, AiSettings? settings = null)
    {
        _settings = settings;
        _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("API Key do Gemini não configurada.");
        _configModel = config["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        _baseUrl = (config["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
        _httpClient = httpClient;
    }

    /// <summary>Modelo escolhido no painel admin (se houver) ou o da configuração.</summary>
    private string Model => _settings?.ModelFor(AiProviders.Find(AiProviders.Gemini)!) ?? _configModel;

    public async Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct) =>
        (await AnswerAsync(question, exactQuote, ct)).Text;

    public async Task<AiAnswer> AnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        string context = string.IsNullOrWhiteSpace(exactQuote) ? "(sem trecho selecionado)" : $"'{exactQuote}'";
        string prompt = $"""
            Você é um assistente de pesquisa integrado a um aplicativo de notas.
            Trecho selecionado pela pessoa: {context}.
            Responda de forma direta e concisa à pergunta abaixo. Se não houver evidência suficiente, diga isso claramente.

            Pergunta: {question}
            """;

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            // Grounding com busca: as fontes vêm em candidates[0].groundingMetadata.groundingChunks.
            tools = new object[] { new { google_search = new { } } },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1beta/models/{Model}:generateContent")
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Corpo de erro não vai para a exceção (pode ecoar o prompt); só o status.
            throw new HttpRequestException($"Falha no Gemini ({(int)response.StatusCode})");
        }

        JsonNode? json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        return Parse(json);
    }

    /// <summary>Extrai texto, fontes (somente URLs http/https, sem duplicatas) e uso de tokens.</summary>
    public static AiAnswer Parse(JsonNode? json)
    {
        JsonNode? candidate = json?["candidates"]?[0];
        string text = string.Join("\n", (candidate?["content"]?["parts"]?.AsArray() ?? [])
            .Select(p => p?["text"]?.GetValue<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        var sources = new List<AiSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonNode? chunk in candidate?["groundingMetadata"]?["groundingChunks"]?.AsArray() ?? [])
        {
            string? uri = chunk?["web"]?["uri"]?.GetValue<string>();
            string title = chunk?["web"]?["title"]?.GetValue<string>() ?? uri ?? string.Empty;
            if (uri is null
                || !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
                || !seen.Add(parsed.ToString()))
            {
                continue;
            }

            sources.Add(new AiSource(title.Length > 300 ? title[..300] : title, parsed.ToString()));
            if (sources.Count == MaxSources)
            {
                break;
            }
        }

        int? input = json?["usageMetadata"]?["promptTokenCount"]?.GetValue<int>();
        int? output = json?["usageMetadata"]?["candidatesTokenCount"]?.GetValue<int>();

        return new AiAnswer(string.IsNullOrWhiteSpace(text) ? "Não foi possível gerar uma resposta." : text, sources, input, output);
    }
}
