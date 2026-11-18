using Microsoft.EntityFrameworkCore;
using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Infrastructure;

/// <summary>
/// Worker recuperável de pesquisa (M12):
/// - reivindica jobs atomicamente (FOR UPDATE SKIP LOCKED) com lease e fencing token;
/// - jobs com lease vencido (worker morto) voltam a ser reivindicados;
/// - falhas fazem retry com backoff até MaxAttempts;
/// - cancelamento é respeitado antes de gravar o resultado;
/// - provider por dono: externo só com consentimento (ADR-019).
/// </summary>
public class DoubtProcessingWorker : BackgroundService
{
    private const int BatchSize = 5;
    /// <summary>Tempo máximo para conferir cada referência declarada pela IA (ADR-045).</summary>
    private static readonly TimeSpan ReferenceTimeout = TimeSpan.FromSeconds(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DoubtProcessingWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}"[..40];
    private readonly TimeSpan _pollInterval;
    private readonly WorkSignal _signal;

    public DoubtProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<DoubtProcessingWorker> logger, IConfiguration config, WorkSignal signal)
    {
        _signal = signal;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(Math.Max(1, config.GetValue<int?>("Research:PollSeconds") ?? 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed = 0;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha no ciclo do worker de pesquisa");
            }

            // Lote cheio: provavelmente há mais trabalho; não espera. Senão, aguarda evento (Outbox/SQS) ou o polling.
            if (processed < BatchSize)
            {
                await _signal.WaitAsync(_pollInterval, stoppingToken);
            }
        }
    }

    private sealed class Claimed
    {
        public Guid Id { get; set; }
        public long FencingToken { get; set; }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset leaseUntil = now + ResearchJob.LeaseDuration;

        // Reivindicação atômica: dois workers nunca pegam o mesmo job.
        List<Claimed> claimed = await db.Database.SqlQuery<Claimed>($"""
            UPDATE "ResearchJobs" SET
                "Status" = 'Running',
                "LeaseOwner" = {_workerId},
                "LeaseExpiresAt" = {leaseUntil},
                "FencingToken" = "FencingToken" + 1,
                "Attempt" = "Attempt" + 1,
                "UpdatedAt" = {now}
            WHERE "Id" IN (
                SELECT "Id" FROM "ResearchJobs"
                WHERE ("Status" = 'Queued' AND "NextAttemptAt" <= {now})
                   OR ("Status" = 'Running' AND "LeaseExpiresAt" < {now})
                ORDER BY "NextAttemptAt"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED)
            RETURNING "Id", "FencingToken"
            """).ToListAsync(ct);

        foreach (Claimed c in claimed)
        {
            await ProcessJobAsync(c, ct);
        }

        return claimed.Count;
    }

    private async Task ProcessJobAsync(Claimed claimed, CancellationToken ct)
    {
        // Escopo (e DbContext) novo por job para não misturar rastreamento entre jobs.
        using IServiceScope jobScope = _scopeFactory.CreateScope();
        var db = jobScope.ServiceProvider.GetRequiredService<NoteDbContext>();
        var selector = jobScope.ServiceProvider.GetRequiredService<AiProviderSelector>();

        ResearchJob? job = await db.ResearchJobs.SingleOrDefaultAsync(j => j.Id == claimed.Id && j.FencingToken == claimed.FencingToken, ct);
        Doubt? doubt = job is null ? null : await db.Doubts.SingleOrDefaultAsync(d => d.Id == job.DoubtId, ct);
        if (job is null || doubt is null)
        {
            return; // reivindicado por outro worker depois de nós, ou dúvida apagada
        }

        // Span por job: correlaciona logs e métricas pelo JobId, sem texto da nota/pergunta.
        using var activity = NoteeTelemetry.Research.StartActivity("research.job.process");
        activity?.SetTag("notee.job.id", job.Id);
        activity?.SetTag("notee.job.attempt", job.Attempt);
        var started = System.Diagnostics.Stopwatch.StartNew();

        UserPreferences prefs = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == job.OwnerId, ct)
                                ?? UserPreferences.Default(job.OwnerId);
        string providerName = selector.Describe(prefs);
        activity?.SetTag("notee.provider", providerName);

        if (job.CancelRequested || !prefs.ResearchEnabled)
        {
            job.MarkCancelled(DateTimeOffset.UtcNow);
            doubt.Cancel();
            await SaveFencedAsync(db, job, ct);
            return;
        }

        doubt.MarkAsRunning();
        await SaveFencedAsync(db, job, ct);

        try
        {
            string quote = doubt.Anchor?.ExactQuote ?? string.Empty;
            var config = jobScope.ServiceProvider.GetRequiredService<IConfiguration>();
            IAiService ai = selector.For(prefs);

            // ADR-046: com o DeepSeek ativo, o Notee pesquisa na web e a resposta usa SÓ os resultados (fontes reais).
            // Sem busca disponível, sem resultado bom ou acima do teto diário: segue pelo caminho do ADR-045.
            GroundedOutcome? grounded = null;
            var searchOptions = WebSearchOptions.From(config);
            if (providerName == AiProviders.DeepSeek && searchOptions.Enabled && await WithinDailySearchCapAsync(db, searchOptions, ct))
            {
                grounded = await GroundedAnswer.TryAnswerAsync(jobScope.ServiceProvider.GetRequiredService<IWebSearch>(), searchOptions,
                    ai.CompleteAsync, doubt.Question, quote, ct);
            }

            string answerText;
            List<ResearchSourceDraft> sourceDrafts;
            int? inputTokens;
            int? outputTokens;
            if (grounded is not null)
            {
                (answerText, sourceDrafts, inputTokens, outputTokens) = (grounded.Text, grounded.Sources, grounded.InputTokens, grounded.OutputTokens);
            }
            else
            {
                AiAnswer answer = await ai.AnswerAsync(doubt.Question, quote, ct);
                (inputTokens, outputTokens) = (answer.InputTokens, answer.OutputTokens);

                // ADR-045: referências declaradas pela IA são conferidas aqui (mesmo resolvedor da criação de nota);
                // só as encontradas ganham link. Cada consulta tem tempo limite; falha = não conferida.
                var http = jobScope.ServiceProvider.GetRequiredService<SafeHttpClient>();
                (answerText, sourceDrafts) = await ResearchReferences.AssembleAsync(answer, async (identifier, token) =>
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(ReferenceTimeout);
                    try
                    {
                        return await SourceResolutionEndpoints.TryResolveAsync(identifier, http, config, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        return null; // tempo esgotado = não conferida
                    }
                }, ct);
            }

            // O usuário pode ter cancelado enquanto o provider respondia.
            await db.Entry(job).ReloadAsync(ct);
            if (job.FencingToken != claimed.FencingToken)
            {
                return; // perdemos o lease: outro worker é o dono agora
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (job.CancelRequested)
            {
                job.MarkCancelled(now);
                doubt.Cancel();
            }
            else
            {
                job.Succeed(providerName, now, inputTokens, outputTokens, grounded?.WebSearches ?? 0);
                // Fontes da nova resposta substituem as da anterior; sem fontes => "Sem fonte".
                db.ResearchSources.RemoveRange(await db.ResearchSources.Where(s => s.DoubtId == doubt.Id).ToListAsync(ct)); // mesma gravação (fencing)
                int position = 0;
                foreach (ResearchSourceDraft source in sourceDrafts)
                {
                    db.ResearchSources.Add((source.Verified && source.Url is not null
                        ? new ResearchSource(doubt.Id, job.Id, position++, source.Title, source.Url)
                        : ResearchSource.Declared(doubt.Id, job.Id, position++, source.Title))
                        .WithSearchMetadata(source.SiteName, source.PublishedAt, source.Authority));
                }

                // "Com fonte" só quando há ao menos uma fonte conferida; origens declaradas não contam.
                doubt.Complete(answerText, providerName, sourceDrafts.Any(s => s.Verified) ? AnswerEvidence.Sourced : AnswerEvidence.NoSource);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Não registrar pergunta/nota no log (privacidade).
            bool willRetry = job.FailAttempt(ex.GetType().Name, DateTimeOffset.UtcNow);
            if (willRetry)
            {
                doubt.MarkQueued();
            }
            else
            {
                doubt.Fail();
            }

            _logger.LogWarning("Job {JobId} falhou (tentativa {Attempt}/{Max}, provider {Provider}): {Error}",
                job.Id, job.Attempt, job.MaxAttempts, providerName, ex.GetType().Name);
        }

        await SaveFencedAsync(db, job, ct);

        var tags = new System.Diagnostics.TagList { { "status", job.Status.ToString() }, { "provider", providerName } };
        NoteeTelemetry.JobsFinished.Add(1, tags);
        NoteeTelemetry.JobDuration.Record(started.Elapsed.TotalMilliseconds, tags);
        activity?.SetTag("notee.job.status", job.Status.ToString());
    }

    /// <summary>FencingToken é token de concorrência: gravação de worker obsoleto falha e é descartada.</summary>
    /// <summary>Teto diário de buscas na web do servidor todo (Research:WebSearch:MaxPerDay; 0 = sem busca).</summary>
    private static async Task<bool> WithinDailySearchCapAsync(NoteDbContext db, WebSearchOptions options, CancellationToken ct)
    {
        if (options.MaxPerDay <= 0)
        {
            return false;
        }

        DateTimeOffset since = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        int used = await db.ResearchJobs.AsNoTracking().Where(j => j.CompletedAt >= since).SumAsync(j => j.WebSearches, ct);
        return used < options.MaxPerDay;
    }

    private async Task SaveFencedAsync(NoteDbContext db, ResearchJob job, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogInformation("Job {JobId}: escrita descartada por fencing (lease perdido)", job.Id);
            db.ChangeTracker.Clear();
        }
    }
}
