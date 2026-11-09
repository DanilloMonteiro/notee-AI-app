using Microsoft.EntityFrameworkCore;
using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Infrastructure;

/// <summary>
/// Processa dúvidas pendentes. O provider é escolhido por dono (ADR-019):
/// sem consentimento para IA externa, usa o Fake determinístico; com pesquisa desligada, não processa.
/// </summary>
public class DoubtProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DoubtProcessingWorker> _logger;

    public DoubtProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<DoubtProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha no ciclo do worker de dúvidas");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<AiProviderSelector>();

        // Dúvidas de contas com pesquisa desligada permanecem pendentes.
        List<Doubt> doubts = await db.Doubts
            .Where(d => d.Status == DoubtStatus.Pending
                        && !db.UserPreferences.Any(p => p.UserId == d.OwnerId && !p.ResearchEnabled))
            .OrderBy(d => d.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (Doubt doubt in doubts)
        {
            UserPreferences prefs = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == doubt.OwnerId, ct)
                                    ?? UserPreferences.Default(doubt.OwnerId);
            IAiService provider = selector.For(prefs);

            doubt.MarkAsRunning();
            await db.SaveChangesAsync(ct);

            try
            {
                string answer = await provider.GetAnswerAsync(doubt.Question, doubt.Anchor?.ExactQuote ?? string.Empty, ct);
                doubt.Complete(answer);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Não registrar texto da nota/pergunta no log (privacidade).
                _logger.LogWarning("Falha ao processar a dúvida {DoubtId} com o provider {Provider}: {Error}",
                    doubt.Id, selector.Describe(prefs), ex.GetType().Name);
                doubt.Fail();
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
