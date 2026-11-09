namespace NoteLab.Domain;

public enum GraphSuggestionStatus
{
    Pending,
    Accepted,
    Dismissed,
    Stale
}

/// <summary>
/// Proposta de conexão gerada para um mapa (MAP-05). Nunca altera o grafo sozinha:
/// só vira aresta com aceite humano individual e revalidação das notas.
/// </summary>
public sealed class GraphSuggestion
{
    public const int ReasonMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string OwnerId { get; private set; }
    public Guid FromNoteId { get; private set; }
    public Guid ToNoteId { get; private set; }
    public EdgeRelationType RelationType { get; private set; }
    public string Reason { get; private set; }
    /// <summary>Versões das duas notas quando a sugestão foi gerada (para detectar sugestão obsoleta).</summary>
    public long FromNoteVersion { get; private set; }
    public long ToNoteVersion { get; private set; }
    public string ContextHash { get; private set; }
    public string Provider { get; private set; }
    public GraphSuggestionStatus Status { get; private set; }
    public Guid? CreatedEdgeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private GraphSuggestion()
    {
        OwnerId = null!;
        Reason = null!;
        ContextHash = null!;
        Provider = null!;
    }

    public GraphSuggestion(Guid boardId, string ownerId, Guid fromNoteId, long fromVersion, Guid toNoteId, long toVersion,
        EdgeRelationType type, string reason, string contextHash, string provider)
    {
        if (fromNoteId == toNoteId)
        {
            throw new DomainException("Sugestão não pode ligar uma nota a ela mesma.");
        }

        Id = Guid.NewGuid();
        BoardId = boardId;
        OwnerId = ownerId;
        FromNoteId = fromNoteId;
        FromNoteVersion = fromVersion;
        ToNoteId = toNoteId;
        ToNoteVersion = toVersion;
        RelationType = type;
        Reason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;
        ContextHash = contextHash;
        Provider = provider;
        Status = GraphSuggestionStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Accept(Guid edgeId)
    {
        EnsurePending();
        Status = GraphSuggestionStatus.Accepted;
        CreatedEdgeId = edgeId;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    public void Dismiss()
    {
        EnsurePending();
        Status = GraphSuggestionStatus.Dismissed;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    public void MarkStale()
    {
        EnsurePending();
        Status = GraphSuggestionStatus.Stale;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != GraphSuggestionStatus.Pending)
        {
            throw new DomainException("A sugestão já foi decidida.");
        }
    }
}
