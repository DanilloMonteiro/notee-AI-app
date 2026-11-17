using System.Text.Json;

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
        if (system.Contains("SOURCE-CITATION-CHECK", StringComparison.Ordinal))
        {
            using JsonDocument request = JsonDocument.Parse(user);
            string excerpt = request.RootElement.GetProperty("sourceExcerpt").GetString() ?? "";
            string evidence = excerpt[..Math.Min(excerpt.Length, 120)];
            string result = JsonSerializer.Serialize(new { verdict = "insufficient", arguments = new[] { new { point = "A simulação não avalia a afirmação.", sourceExcerpt = evidence } } });
            return Task.FromResult(new AiCompletion(result, Math.Max(1, user.Length / 4), Math.Max(1, result.Length / 4)));
        }
        if (system.StartsWith(NoteComposer.Marker, StringComparison.Ordinal))
        {
            string composed = NoteComposer.FakeCompletion(user);
            return Task.FromResult(new AiCompletion(composed, Math.Max(1, user.Length / 4), Math.Max(1, composed.Length / 4)));
        }
        string? note = NoteAiOrganizer.ExtractNote(user);
        return note is null
            ? Task.FromException<AiCompletion>(new NotSupportedException("Tarefa não suportada pelo provedor simulado."))
            : Task.FromResult(new AiCompletion(NoteAiOrganizer.FakeOrganize(note)));
    }
}
