namespace NoteLab.Domain;

public enum ResearchJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// Job durável de pesquisa (M09/M12). A própria tabela é a fila: o worker reivindica jobs com
/// lease + fencing token (FOR UPDATE SKIP LOCKED), faz retry com backoff e respeita cancelamento.
/// </summary>
public sealed class ResearchJob
{
    public const int DefaultMaxAttempts = 3;
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public Guid Id { get; private set; }
    public Guid DoubtId { get; private set; }
    public string OwnerId { get; private set; }
    public ResearchJobStatus Status { get; private set; }
    public int Attempt { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    /// <summary>Incrementado a cada reivindicação; um worker com token antigo não consegue gravar.</summary>
    public long FencingToken { get; private set; }
    public bool CancelRequested { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? Provider { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private ResearchJob()
    {
        OwnerId = null!;
    }

    public ResearchJob(Guid doubtId, string ownerId, string? idempotencyKey, int maxAttempts = DefaultMaxAttempts)
    {
        if (doubtId == Guid.Empty || string.IsNullOrWhiteSpace(ownerId))
        {
            throw new DomainException("Dúvida e dono são obrigatórios.");
        }

        if (idempotencyKey is { Length: > 100 })
        {
            throw new DomainException("Idempotency-Key deve ter até 100 caracteres.");
        }

        Id = Guid.NewGuid();
        DoubtId = doubtId;
        OwnerId = ownerId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        MaxAttempts = Math.Max(1, maxAttempts);
        Status = ResearchJobStatus.Queued;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        NextAttemptAt = CreatedAt;
    }

    public bool IsActive => Status is ResearchJobStatus.Queued or ResearchJobStatus.Running;

    /// <summary>Reivindicação feita em memória (o worker usa SQL atômico equivalente).</summary>
    public void Claim(string worker, DateTimeOffset now)
    {
        if (!(Status == ResearchJobStatus.Queued && NextAttemptAt <= now)
            && !(Status == ResearchJobStatus.Running && LeaseExpiresAt < now))
        {
            throw new DomainException("Job não está disponível para processamento.");
        }

        Status = ResearchJobStatus.Running;
        LeaseOwner = worker;
        LeaseExpiresAt = now + LeaseDuration;
        FencingToken++;
        Attempt++;
        UpdatedAt = now;
    }

    public void Succeed(string provider, DateTimeOffset now)
    {
        EnsureRunning();
        Status = ResearchJobStatus.Succeeded;
        Provider = provider;
        LastError = null;
        ReleaseLease(now);
        CompletedAt = now;
    }

    /// <summary>Falha transitória: reagenda com backoff exponencial ou falha definitivamente.</summary>
    /// <returns>true se ainda haverá nova tentativa.</returns>
    public bool FailAttempt(string error, DateTimeOffset now)
    {
        EnsureRunning();
        LastError = error.Length > 300 ? error[..300] : error;
        ReleaseLease(now);

        if (Attempt >= MaxAttempts)
        {
            Status = ResearchJobStatus.Failed;
            CompletedAt = now;
            return false;
        }

        Status = ResearchJobStatus.Queued;
        NextAttemptAt = now + Backoff(Attempt);
        return true;
    }

    /// <summary>Na fila: cancela já. Em execução: sinaliza para o worker abandonar o resultado.</summary>
    /// <returns>true se o cancelamento foi imediato.</returns>
    public bool RequestCancel(DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new DomainException("O job já terminou e não pode ser cancelado.");
        }

        if (Status == ResearchJobStatus.Queued)
        {
            MarkCancelled(now);
            return true;
        }

        CancelRequested = true;
        UpdatedAt = now;
        return false;
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        Status = ResearchJobStatus.Cancelled;
        ReleaseLease(now);
        CompletedAt = now;
    }

    public static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(10 * Math.Pow(2, Math.Max(0, attempt - 1)));

    private void ReleaseLease(DateTimeOffset now)
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    private void EnsureRunning()
    {
        if (Status != ResearchJobStatus.Running)
        {
            throw new DomainException("Job não está em execução.");
        }
    }
}
