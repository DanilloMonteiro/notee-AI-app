namespace NoteLab.Domain;

/// <summary>
/// Relação dirigida criada pelo autor entre duas notas, válida apenas neste mapa.
/// Representa uma associação do autor, não um fato verificado.
/// </summary>
public sealed class BoardEdge
{
    public const int LabelMaxLength = 80;

    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid FromNoteId { get; private set; }
    public Guid ToNoteId { get; private set; }
    public EdgeRelationType RelationType { get; private set; }
    public string? Label { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BoardEdge() { }

    public BoardEdge(Guid boardId, Guid fromNoteId, Guid toNoteId, EdgeRelationType relationType, string? label)
    {
        if (boardId == Guid.Empty)
        {
            throw new DomainException("Board é obrigatório para criar uma conexão.");
        }

        Id = Guid.NewGuid();
        BoardId = boardId;
        Apply(fromNoteId, toNoteId, relationType, label);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(Guid fromNoteId, Guid toNoteId, EdgeRelationType relationType, string? label)
    {
        Apply(fromNoteId, toNoteId, relationType, label);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Mesma origem, destino, tipo e rótulo (comparação sem diferenciar maiúsculas).</summary>
    public bool IsEquivalentTo(Guid fromNoteId, Guid toNoteId, EdgeRelationType relationType, string? label) =>
        FromNoteId == fromNoteId
        && ToNoteId == toNoteId
        && RelationType == relationType
        && string.Equals(Label ?? string.Empty, NormalizeLabel(label) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    public static string? NormalizeLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? null : label.Trim();

    private void Apply(Guid fromNoteId, Guid toNoteId, EdgeRelationType relationType, string? label)
    {
        if (fromNoteId == Guid.Empty || toNoteId == Guid.Empty)
        {
            throw new DomainException("Origem e destino são obrigatórios.");
        }

        if (fromNoteId == toNoteId)
        {
            throw new DomainException("Uma nota não pode ser conectada a ela mesma.");
        }

        if (!Enum.IsDefined(relationType))
        {
            throw new DomainException("Tipo de relação inválido.");
        }

        string? normalized = NormalizeLabel(label);
        if (normalized is { Length: > LabelMaxLength })
        {
            throw new DomainException($"O rótulo deve ter até {LabelMaxLength} caracteres.");
        }

        FromNoteId = fromNoteId;
        ToNoteId = toNoteId;
        RelationType = relationType;
        Label = normalized;
    }
}
