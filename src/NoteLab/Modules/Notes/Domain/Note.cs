using System.Numerics;

namespace NoteLab.Domain;

public sealed class Note
{
    public Guid Id { get; private set; }

    public NoteTitle Title { get; private set; }
    
    public string? Content { get; private set; }

    public string OwnerId {get; private set; }

    public NoteStatus Status { get; private set; }

    public long Version {get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
    
    public Note(
        NoteTitle title,
        string ownerId,
        string? content)
    {
        Id = Guid.NewGuid();
        Title = title;
        OwnerId = ownerId;
        Content = content;
        Status = NoteStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(NoteTitle title, string? content)
    {
        Title = title;
        Content = content;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        if (Status == NoteStatus.Archived)
        {
            throw new DomainException("Cannot archive a note that is already archived");
        } 

        Status = NoteStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restore() {
        Status = NoteStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }


}