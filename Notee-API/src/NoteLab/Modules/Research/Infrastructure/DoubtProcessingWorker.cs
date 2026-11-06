using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NoteLab.Domain;
using NoteLab.Application;
using Microsoft.EntityFrameworkCore;

namespace NoteLab.Infrastructure;

public class DoubtProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DoubtProcessingWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Este loop roda para sempre enquanto a aplicação estiver viva
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Criamos um escopo temporário (como se fosse uma requisição HTTP)
            using (var scope = _scopeFactory.CreateScope())
            {
                // 2. Pedimos as nossas dependências para este escopo
                var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();

                // SUA MISSÃO COMEÇA AQUI:

                // a) Busque no banco (db.Doubts) as dúvidas onde Status == DoubtStatus.Pending.
                List<Doubt> doubts = await db.Doubts.Where(d => d.Status == DoubtStatus.Pending).Take(10).ToListAsync(stoppingToken);

                // b) Faça um foreach nessas dúvidas.
                foreach(var doubt in doubts) {
                    // c) Chame doubt.MarkAsRunning() e dê um SaveChanges.
                    doubt.MarkAsRunning();
                    await db.SaveChangesAsync(stoppingToken);

                    try
                    {
                        // d) Chame o aiService.GetAnswerAsync(...)
                        string respostaDaIA = await aiService.GetAnswerAsync(doubt.Question, doubt.Anchor.ExactQuote, stoppingToken);

                        // e) Chame doubt.Complete(respostaDaIA) e dê um SaveChanges final.
                        doubt.Complete(respostaDaIA);
                    }
                    catch (Exception ex)
                    {
                        // Se a IA falhar (ex: erro 404, sem internet, etc.), o sistema não morre!
                        Console.WriteLine($"[AVISO] Falha ao processar a dúvida {doubt.Id}: {ex.Message}");
                        doubt.Fail(); // Lembra-se do método Fail que criámos na entidade?
                    }
                   
                    await db.SaveChangesAsync(stoppingToken);
                }
                
            }

            // Pausa de 10 segundos antes de procurar de novo
            await Task.Delay(10000, stoppingToken);
        }
    }
}