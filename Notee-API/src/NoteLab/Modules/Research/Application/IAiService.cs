namespace NoteLab.Application;
public interface IAiService
{
    public Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct);
}