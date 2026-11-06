namespace NoteLab.Domain;

public sealed class Doubt
{
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public Guid NoteId { get; private set; }
    public long NoteRevision { get; private set; }
    public DoubtAnchor Anchor { get; private set; }
    public string Question { get; private set; }
    public string? Answer { get; private set; }
    public DoubtStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Doubt() 
    { 
        OwnerId = null!;
        Anchor = null!;
        Question = null!;
    }
    public Doubt(string ownerId, Guid noteId, long noteRevision, DoubtAnchor anchor, string question)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        NoteId = noteId;
        NoteRevision = noteRevision;
        Anchor = anchor;
        Question = question;
        Status = DoubtStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsRunning()
    {
        Status = DoubtStatus.Running;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(string answer)
    {
        Answer = answer;
        Status = DoubtStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fail()
    {
        Status = DoubtStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}