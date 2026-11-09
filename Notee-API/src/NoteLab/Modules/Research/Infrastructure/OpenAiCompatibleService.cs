using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

/// <summary>
/// Adaptador para APIs no formato "chat completions" (OpenAI ChatGPT, xAI Grok, DeepSeek).
/// Esses endpoints não devolvem citações verificáveis, então as respostas saem "Sem fonte" — nunca inventamos fontes.
/// Usado SOMENTE com consentimento da conta (ADR-019).
/// </summary>
public abstract class OpenAiCompatibleService : IAiService
{
    private readonly HttpClient _http;
    private readonly AiSettings _settings;
    private readonly AiProviderInfo _provider;

    protected OpenAiCompatibleService(HttpClient http, AiSettings settings, string providerId)
    {
        _http = http;
        _settings = settings;
        _provider = AiProviders.Find(providerId) ?? throw new ArgumentException(providerId);
    }

    public async Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct) =>
        (await AnswerAsync(question, exactQuote, ct)).Text;

    public async Task<AiAnswer> AnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        string apiKey = _settings.ApiKeyFor(_provider) is { } key && AiProviders.IsRealKey(key)
            ? key
            : throw new InvalidOperationException($"{_provider.Name} sem chave configurada.");

        var body = new
        {
            model = _settings.ModelFor(_provider),
            messages = new object[]
            {
                new { role = "system", content = AiPrompts.System },
                new { role = "user", content = AiPrompts.User(question, exactQuote) },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrlFor(_provider)}/chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // O corpo do erro não é propagado (pode ecoar o prompt).
            throw new HttpRequestException($"Falha no {_provider.Name} ({(int)response.StatusCode})");
        }

        return Parse(JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)));
    }

    public static AiAnswer Parse(JsonNode? json)
    {
        string? text = json?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        int? input = json?["usage"]?["prompt_tokens"]?.GetValue<int>();
        int? output = json?["usage"]?["completion_tokens"]?.GetValue<int>();
        return new AiAnswer(string.IsNullOrWhiteSpace(text) ? "Não foi possível gerar uma resposta." : text.Trim(), [], input, output);
    }
}

public sealed class ChatGptAiService(HttpClient http, AiSettings settings) : OpenAiCompatibleService(http, settings, AiProviders.ChatGpt);

public sealed class GrokAiService(HttpClient http, AiSettings settings) : OpenAiCompatibleService(http, settings, AiProviders.Grok);

public sealed class DeepSeekAiService(HttpClient http, AiSettings settings) : OpenAiCompatibleService(http, settings, AiProviders.DeepSeek);

/// <summary>Prompt comum aos provedores: só a pergunta e o trecho, nunca a nota inteira.</summary>
public static class AiPrompts
{
    public const string System =
        "Você é um assistente de pesquisa integrado a um aplicativo de notas. Responda em português, de forma direta e concisa. " +
        "Se não houver evidência suficiente, diga isso claramente. Não invente fontes.";

    public static string User(string question, string exactQuote) =>
        $"Trecho selecionado pela pessoa: {(string.IsNullOrWhiteSpace(exactQuote) ? "(sem trecho selecionado)" : $"'{exactQuote}'")}.\n\nPergunta: {question}";
}
