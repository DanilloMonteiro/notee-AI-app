using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

public class OpenAiService : IAiService
{
    private readonly ChatClient _client;

    public OpenAiService(IConfiguration config)
    {
        // Vai buscar a chave ao appsettings.json de forma segura
        string apiKey = config["OpenAI:ApiKey"] ?? throw new ArgumentNullException("API Key da OpenAI não configurada.");
        
        // Inicializa o cliente para o modelo gpt-4o-mini (rápido e barato)
        _client = new ChatClient(model: "gpt-4o-mini", apiKey: apiKey);
    }

    public async Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        // Montamos a personalidade e o contexto da IA
        string systemPrompt = $@"
            És um assistente de escrita inteligente integrado numa aplicação de notas. 
            O utilizador tem uma dúvida sobre o seguinte trecho de texto que ele selecionou: '{exactQuote}'.
            Responde de forma direta, clara e concisa à pergunta do utilizador, tendo este texto como contexto.
        ";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(question)
        };

        // Fazemos a chamada real à API
        ChatCompletion completion = await _client.CompleteChatAsync(messages, cancellationToken: ct);

        // Retornamos o texto gerado
        return completion.Content[0].Text;
    }
}