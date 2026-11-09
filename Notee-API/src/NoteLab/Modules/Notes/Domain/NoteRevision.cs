namespace NoteLab.Domain;

public enum RevisionReason
{
    /// <summary>Estado anterior registrado na primeira edição de notas criadas antes do histórico existir.</summary>
    Baseline,
    Created,
    Edited,
    Restored
}

/// <summary>
/// Snapshot imutável do conteúdo da nota numa versão (notes.revisions no planejamento).
/// Mudanças de estado (arquivar/lixeira) e de layout de mapas não geram revisão.
/// </summary>
public sealed class NoteRevision
{
    public Guid Id { get; private set; }
    public Guid NoteId { get; private set; }
    public string OwnerId { get; private set; }
    public long NoteVersion { get; private set; }
    public string Title { get; private set; }
    public string? Content { get; private set; }
    public Guid? CategoryId { get; private set; }
    /// <summary>IDs das tags no momento do snapshot, separados por vírgula.</summary>
    public string TagIds { get; private set; }
    public RevisionReason Reason { get; private set; }
    public long? RestoredFromVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private NoteRevision()
    {
        OwnerId = null!;
        Title = null!;
        TagIds = null!;
    }

    private NoteRevision(Note note, long version, RevisionReason reason, long? restoredFrom)
    {
        Id = Guid.NewGuid();
        NoteId = note.Id;
        OwnerId = note.OwnerId;
        NoteVersion = version;
        Title = note.Title.Value;
        Content = note.Content;
        CategoryId = note.CategoryId;
        TagIds = string.Join(',', note.Tags.Select(t => t.Id).OrderBy(id => id));
        Reason = reason;
        RestoredFromVersion = restoredFrom;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Snapshot do estado atual da nota (já com a versão final).</summary>
    public static NoteRevision Capture(Note note, RevisionReason reason, long? restoredFrom = null) =>
        new(note, note.Version, reason, restoredFrom);

    /// <summary>Snapshot do estado anterior a uma edição, com a versão anterior.</summary>
    public static NoteRevision CaptureBaseline(Note note) =>
        new(note, note.Version, RevisionReason.Baseline, null);

    public IReadOnlyList<Guid> GetTagIds() =>
        string.IsNullOrEmpty(TagIds) ? [] : TagIds.Split(',').Select(Guid.Parse).ToList();
}
