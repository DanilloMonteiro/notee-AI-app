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
    public Guid? CategoryId { get; private set; }    
    private readonly List<Tag> _tags = new();
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();
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

    public void SetCategory(Category category)
    {
        if (category == null)
        {
            throw new DomainException("Cannot set a null category to a note");
        }

        if (category.OwnerId != OwnerId)
        {
            throw new DomainException("Cannot set a category of other owner to this note");
        }
    
        CategoryId = category.Id;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddTag(Tag tag)
    {
        if (tag == null)
        {
            throw new DomainException("Cannot set a null tag to a note");
        }

        if (tag.OwnerId != OwnerId)
        {
            throw new DomainException("Cannot set a tag of other owner to this note");
        }

        if (_tags.Any(t => t.Id == tag.Id)) return;

        _tags.Add(tag);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveTag(Tag tag)
    {
        if (tag == null)
        {
            throw new DomainException("Cannot set a null tag to a note");
        }

        if (tag.OwnerId != OwnerId)
        {
            throw new DomainException("Cannot set a tag of other owner to this note");
        }  

        if (!_tags.Any(t => t.Id == tag.Id)) return;

        _tags.Remove(tag);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}