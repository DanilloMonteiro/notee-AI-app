using System.Text.Json.Nodes;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiAiService(IConfiguration config, HttpClient httpClient)
    {
        _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("API Key do Gemini não configurada.");
        _httpClient = httpClient;
    }

    public async Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        // 1. Montamos o prompt juntando o contexto e a pergunta
        string prompt = $@"
            És um assistente de escrita inteligente integrado numa aplicação de notas. 
            O utilizador tem uma dúvida sobre o seguinte trecho de texto que ele selecionou: '{exactQuote}'.
            Responde de forma direta, clara e concisa à pergunta do utilizador, tendo este texto como contexto.
            
            Pergunta do utilizador: {question}
        ";

        // 2. Definimos o endpoint do modelo (gemini-1.5-flash é o mais rápido e gratuito)
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key={_apiKey}";

        // 3. Montamos o corpo da requisição no formato exigido pela Google
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

       // 4. Disparamos a requisição HTTP
        var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
        
        // Se falhar, lemos a mensagem de erro da Google em vez de "rebentar" cegamente
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Falha no Gemini ({response.StatusCode}): {errorBody}");
        }

        // 5. Lemos a resposta JSON de sucesso
        var jsonString = await response.Content.ReadAsStringAsync(ct);
        var json = JsonNode.Parse(jsonString);
        
        string? answer = json?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

        return answer ?? "Não foi possível gerar uma resposta.";
    }
}