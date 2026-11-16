using System.Text.Json;

namespace NoteLab.Domain;

public enum SourceCheckLayer { Existence, Citation, Ai }
public enum SourceCheckStatus { Pending, Running, Confirmed, FoundWithDifferences, QuoteNotFound, Mismatch, NotFound, Unverifiable, Failed }

/// <summary>Resultado auditável de uma camada de verificação da fonte.</summary>
public sealed class SourceCheck
{
    public const int SummaryMaxLength = 1000;
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public Guid? CitationId { get; private set; }
    public SourceCheckLayer Layer { get; private set; }
    public SourceCheckStatus Status { get; private set; }
    public string Summary { get; private set; } = "";
    public string ArgumentsJson { get; private set; } = "[]";
    public string? Provider { get; private set; }
    public string? Verdict { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CheckedAt { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public long FencingToken { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public decimal? CostUsd { get; private set; }

    private SourceCheck() { }

    public SourceCheck(string ownerId, Guid sourceId, SourceCheckLayer layer, Guid? citationId = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || sourceId == Guid.Empty) throw new DomainException("Dono e fonte são obrigatórios.");
        Id = Guid.NewGuid(); OwnerId = ownerId; SourceId = sourceId; Layer = layer; CitationId = citationId;
        Status = SourceCheckStatus.Pending; CreatedAt = DateTimeOffset.UtcNow; NextAttemptAt = CreatedAt;
    }

    public void Claim(string workerId, DateTimeOffset now, TimeSpan lease)
    {
        if (Status == SourceCheckStatus.Running && LeaseExpiresAt > now) throw new DomainException("A checagem já está em execução.");
        Status = SourceCheckStatus.Running; LeaseOwner = workerId[..Math.Min(workerId.Length, 64)]; LeaseExpiresAt = now + lease; Attempts++; FencingToken++;
    }

    public void Complete(SourceCheckStatus status, string summary, object? arguments, string? provider = null,
        int? inputTokens = null, int? outputTokens = null, decimal? costUsd = null, string? verdict = null)
    {
        if (status is SourceCheckStatus.Pending or SourceCheckStatus.Running) throw new DomainException("Resultado final inválido.");
        Summary = (summary ?? "").Trim();
        if (Summary.Length > SummaryMaxLength) Summary = Summary[..SummaryMaxLength];
        ArgumentsJson = JsonSerializer.Serialize(arguments ?? Array.Empty<object>());
        Provider = string.IsNullOrWhiteSpace(provider) ? null : provider[..Math.Min(provider.Length, 100)];
        InputTokens = inputTokens; OutputTokens = outputTokens; CostUsd = costUsd;
        Verdict = verdict;
        Status = status; CheckedAt = DateTimeOffset.UtcNow; LeaseExpiresAt = null; LeaseOwner = null;
        NextAttemptAt = DateTimeOffset.UtcNow;
    }

    public void Retry(string summary)
    {
        Summary = summary.Length > SummaryMaxLength ? summary[..SummaryMaxLength] : summary;
        Status = SourceCheckStatus.Pending;
        LeaseExpiresAt = null; LeaseOwner = null;
        NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, Math.Clamp(Attempts, 1, 6))));
    }
}

/// <summary>Arquivo privado da fonte ou texto extraído. O objeto permanece fora do banco.</summary>
public sealed class SourceArchive
{
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public string Kind { get; private set; } = "file";
    public string StorageKey { get; private set; } = null!;
    public string? TextKey { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public string Basis { get; private set; } = "user-upload";
    public string? OriginUrl { get; private set; }
    public string? SnapshotUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private SourceArchive() { }

    public SourceArchive(string ownerId, Guid sourceId, string storageKey, string? textKey,
        string contentType, long sizeBytes, string sha256, string basis = "user-upload", string kind = "file")
    {
        if (string.IsNullOrWhiteSpace(ownerId) || sourceId == Guid.Empty ||
            (kind == "snapshot" ? sizeBytes != 0 : sizeBytes <= 0) || kind is not ("file" or "snapshot"))
            throw new DomainException("Dono, fonte e tamanho de arquivo são obrigatórios.");
        Id = Guid.NewGuid(); OwnerId = ownerId; SourceId = sourceId; Kind = kind; StorageKey = storageKey;
        TextKey = textKey; ContentType = contentType; SizeBytes = sizeBytes; Sha256 = sha256;
        Basis = basis; CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSnapshot(string? originUrl, string? snapshotUrl)
    {
        OriginUrl = originUrl; SnapshotUrl = snapshotUrl;
    }
}

public sealed record SourceCheckArgument(string Point, string Evidence, string? Link = null);
public sealed record SourceCheckResult(SourceCheckStatus Status, string Summary, IReadOnlyList<SourceCheckArgument> Arguments, string? Provider = null);
public sealed record ExtractedPage(int Page, string Text);
