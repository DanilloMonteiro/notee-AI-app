namespace NoteLab.Application;

/// <summary>
/// Provider determinístico usado por padrão: não chama nenhum serviço externo e
/// não inventa fontes. A mesma entrada gera sempre a mesma resposta.
/// </summary>
public class FakeAiService : IAiService
{
    public const string Marker = "[Resposta simulada]";

    public Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        string context = string.IsNullOrWhiteSpace(exactQuote) ? "sem trecho selecionado" : $"trecho: “{exactQuote.Trim()}”";
        string answer =
            $"{Marker} Nenhum serviço externo foi consultado ({context}). " +
            $"Pergunta registrada: “{question.Trim()}”. " +
            "Sem fonte. Para respostas reais, autorize o uso de IA externa em Configurações.";
        return Task.FromResult(answer);
    }

    /// <summary>Simulação local e sem custo do "Organizar com IA" (ADR-037); nada sai do servidor.</summary>
    public Task<AiCompletion> CompleteAsync(string system, string user, int maxOutputTokens, CancellationToken ct)
    {
        string? note = NoteAiOrganizer.ExtractNote(user);
        return note is null
            ? Task.FromException<AiCompletion>(new NotSupportedException("Tarefa não suportada pelo provedor simulado."))
            : Task.FromResult(new AiCompletion(NoteAiOrganizer.FakeOrganize(note)));
    }
}
