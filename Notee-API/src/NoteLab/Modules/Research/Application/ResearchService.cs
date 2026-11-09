using Microsoft.EntityFrameworkCore;
using Npgsql;
using NoteLab.Domain;

namespace NoteLab.Application;

/// <summary>
/// Casos de uso de dúvidas e jobs de pesquisa. Criar a dúvida e o job acontece na MESMA transação,
/// então nenhuma dúvida aceita fica sem job (equivalente local ao Outbox — ADR-023).
/// </summary>
public sealed class ResearchService
{
    private readonly NoteDbContext _db;
    private readonly int _maxJobsPerHour;

    public ResearchService(NoteDbContext db, IConfiguration config)
    {
        _db = db;
        // Cota por conta (M14): protege custo do provider e abuso.
        _maxJobsPerHour = Math.Max(1, config.GetValue<int?>("Research:MaxJobsPerHour") ?? 60);
    }

    public async Task<MapResult<(Doubt Doubt, ResearchJob? Job)>> CreateSelectionDoubtAsync(string ownerId, CreateDoubtRequest request, CancellationToken ct)
    {
        Note? note = await EditableNoteAsync(ownerId, request.NoteId, ct);
        if (note is null)
        {
            return MapResult<(Doubt, ResearchJob?)>.NotFound("Nota não encontrada.");
        }

        Doubt doubt;
        try
        {
            doubt = new Doubt(ownerId, note.Id, note.Version,
                new DoubtAnchor(request.BlockId, request.ExactQuote, request.StartIndex, request.EndIndex), request.Question);
        }
        catch (DomainException ex)
        {
            return MapResult<(Doubt, ResearchJob?)>.Invalid(ex.Message);
        }

        return MapResult<(Doubt, ResearchJob?)>.Ok(await AddWithJobAsync(doubt, ct));
    }

    /// <summary>Dúvida livre criada no mapa (AC09): origem BoardQuestion, sem âncora.</summary>
    public async Task<MapResult<(Doubt Doubt, ResearchJob? Job)>> CreateBoardDoubtAsync(string ownerId, Guid noteId, string question, CancellationToken ct)
    {
        Note? note = await EditableNoteAsync(ownerId, noteId, ct);
        if (note is null)
        {
            return MapResult<(Doubt, ResearchJob?)>.NotFound("Nota não encontrada.");
        }

        Doubt doubt;
        try
        {
            doubt = Doubt.FromBoard(ownerId, note.Id, note.Version, question);
        }
        catch (DomainException ex)
        {
            return MapResult<(Doubt, ResearchJob?)>.Invalid(ex.Message);
        }

        return MapResult<(Doubt, ResearchJob?)>.Ok(await AddWithJobAsync(doubt, ct));
    }

    /// <summary>
    /// Enfileira pesquisa. Idempotente: mesma Idempotency-Key devolve o mesmo job; se já houver job ativo, devolve-o.
    /// </summary>
    public async Task<MapResult<ResearchJob>> EnqueueAsync(string ownerId, Guid doubtId, string? idempotencyKey, CancellationToken ct)
    {
        Doubt? doubt = await _db.Doubts.SingleOrDefaultAsync(d => d.Id == doubtId && d.OwnerId == ownerId, ct);
        if (doubt is null)
        {
            return MapResult<ResearchJob>.NotFound("Dúvida não encontrada.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            string key = idempotencyKey.Trim();
            ResearchJob? same = await _db.ResearchJobs.SingleOrDefaultAsync(j => j.OwnerId == ownerId && j.IdempotencyKey == key, ct);
            if (same is not null)
            {
                return same.DoubtId == doubtId
                    ? MapResult<ResearchJob>.Ok(same)
                    : MapResult<ResearchJob>.Conflict("Idempotency-Key já usada para outra dúvida.");
            }
        }

        ResearchJob? active = await _db.ResearchJobs
            .Where(j => j.DoubtId == doubtId && (j.Status == ResearchJobStatus.Queued || j.Status == ResearchJobStatus.Running))
            .FirstOrDefaultAsync(ct);
        if (active is not null)
        {
            return MapResult<ResearchJob>.Ok(active);
        }

        UserPreferences prefs = await PreferencesAsync(ownerId, ct);
        if (!prefs.ResearchEnabled)
        {
            return MapResult<ResearchJob>.Conflict("A pesquisa está desligada em Configurações.");
        }

        if (await QuotaExceededAsync(ownerId, ct))
        {
            return MapResult<ResearchJob>.TooMany($"Limite de {_maxJobsPerHour} pesquisas por hora atingido. Tente mais tarde.");
        }

        ResearchJob job;
        try
        {
            job = new ResearchJob(doubt.Id, ownerId, idempotencyKey);
        }
        catch (DomainException ex)
        {
            return MapResult<ResearchJob>.Invalid(ex.Message);
        }

        doubt.MarkQueued();
        _db.ResearchJobs.Add(job);
        NoteeTelemetry.JobsEnqueued.Add(1, new KeyValuePair<string, object?>("trigger", "manual"));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre duas requisições com a mesma chave ou dois jobs ativos.
            return MapResult<ResearchJob>.Conflict("Já existe uma pesquisa em andamento para esta dúvida.");
        }

        return MapResult<ResearchJob>.Ok(job);
    }

    public Task<ResearchJob?> GetJobAsync(string ownerId, Guid jobId, CancellationToken ct) =>
        _db.ResearchJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.OwnerId == ownerId, ct);

    public async Task<MapResult<ResearchJob>> CancelAsync(string ownerId, Guid jobId, CancellationToken ct)
    {
        ResearchJob? job = await _db.ResearchJobs.SingleOrDefaultAsync(j => j.Id == jobId && j.OwnerId == ownerId, ct);
        if (job is null)
        {
            return MapResult<ResearchJob>.NotFound("Pesquisa não encontrada.");
        }

        try
        {
            bool immediate = job.RequestCancel(DateTimeOffset.UtcNow);
            if (immediate)
            {
                Doubt? doubt = await _db.Doubts.SingleOrDefaultAsync(d => d.Id == job.DoubtId, ct);
                doubt?.Cancel();
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (DomainException ex)
        {
            return MapResult<ResearchJob>.Conflict(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return MapResult<ResearchJob>.Conflict("A pesquisa mudou de estado. Tente novamente.");
        }

        return MapResult<ResearchJob>.Ok(job);
    }

    public async Task<Dictionary<Guid, ResearchJob>> LatestJobsAsync(IEnumerable<Guid> doubtIds, CancellationToken ct)
    {
        List<Guid> ids = doubtIds.Distinct().ToList();
        List<ResearchJob> jobs = await _db.ResearchJobs.AsNoTracking()
            .Where(j => ids.Contains(j.DoubtId))
            .ToListAsync(ct);
        return jobs.GroupBy(j => j.DoubtId).ToDictionary(g => g.Key, g => g.OrderByDescending(j => j.CreatedAt).First());
    }

    private async Task<(Doubt, ResearchJob?)> AddWithJobAsync(Doubt doubt, CancellationToken ct)
    {
        _db.Doubts.Add(doubt);

        // Pesquisa automática ao criar, salvo se a conta desligou a pesquisa (fica pendente).
        ResearchJob? job = null;
        // Precedência: pesquisa desligada (conta) > configuração da nota > padrão da conta (M10).
        // Acima da cota, a dúvida é registrada mas fica pendente (pode ser pesquisada depois).
        AutoResearchOverride noteOverride = await _db.NoteSettings.AsNoTracking()
            .Where(s => s.NoteId == doubt.NoteId && s.OwnerId == doubt.OwnerId)
            .Select(s => s.AutoResearch)
            .FirstOrDefaultAsync(ct);
        if (NoteSettings.ShouldAutoResearch(await PreferencesAsync(doubt.OwnerId, ct), noteOverride) && !await QuotaExceededAsync(doubt.OwnerId, ct))
        {
            job = new ResearchJob(doubt.Id, doubt.OwnerId, null);
            _db.ResearchJobs.Add(job);
            NoteeTelemetry.JobsEnqueued.Add(1, new KeyValuePair<string, object?>("trigger", "auto"));
        }

        await _db.SaveChangesAsync(ct);
        return (doubt, job);
    }

    private async Task<bool> QuotaExceededAsync(string ownerId, CancellationToken ct)
    {
        DateTimeOffset since = DateTimeOffset.UtcNow.AddHours(-1);
        return await _db.ResearchJobs.CountAsync(j => j.OwnerId == ownerId && j.CreatedAt >= since, ct) >= _maxJobsPerHour;
    }

    private Task<Note?> EditableNoteAsync(string ownerId, Guid noteId, CancellationToken ct) =>
        _db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == ownerId && n.Status != NoteStatus.Trashed, ct);

    private async Task<UserPreferences> PreferencesAsync(string ownerId, CancellationToken ct) =>
        await _db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == ownerId, ct)
        ?? UserPreferences.Default(ownerId);
}
