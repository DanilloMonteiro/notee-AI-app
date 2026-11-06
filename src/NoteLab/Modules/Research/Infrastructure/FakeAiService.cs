using Microsoft.EntityFrameworkCore;

namespace NoteLab.Application;
public class FakeAiService : IAiService
{
    public FakeAiService()
    {
    }
    async public Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        await Task.Delay(2000);

        return "String";
    }
} 