namespace NoteLab.Domain;

public sealed class Note
{
    public Guid Id { get; private set; }
    public NoteTitle Title { get; private set; }
    public string? Content { get; private set; }
    /// <summary>Texto simples extraído no servidor (para busca), sem a estrutura JSON dos blocos.</summary>
    public string? PlainText { get; private set; }
    public string OwnerId { get; private set; }
    public NoteStatus Status { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? TrashedAt { get; private set; }
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

    /// <summary>Notas na lixeira não podem ser editadas; restaure antes.</summary>
    public bool IsEditable => Status != NoteStatus.Trashed;

    public void Update(NoteTitle title, string? content)
    {
        EnsureEditable();
        Title = title;
        Content = content;
    }

    public void Archive()
    {
        if (Status == NoteStatus.Archived)
        {
            throw new DomainException("Cannot archive a note that is already archived");
        }

        if (Status == NoteStatus.Trashed)
        {
            throw new DomainException("Restaure a nota da lixeira antes de arquivá-la.");
        }

        Status = NoteStatus.Archived;
        RegisterUpdate();
    }

    /// <summary>Envia para a lixeira (reversível). Exclusão definitiva só a partir daqui.</summary>
    public void MoveToTrash()
    {
        if (Status == NoteStatus.Trashed)
        {
            throw new DomainException("A nota já está na lixeira.");
        }

        Status = NoteStatus.Trashed;
        TrashedAt = DateTimeOffset.UtcNow;
        RegisterUpdate();
    }

    /// <summary>Volta de Arquivada/Lixeira para Ativa.</summary>
    public void Restore()
    {
        if (Status is not (NoteStatus.Archived or NoteStatus.Trashed))
        {
            throw new DomainException("Somente notas arquivadas ou na lixeira podem ser restauradas.");
        }

        Status = NoteStatus.Active;
        TrashedAt = null;
        RegisterUpdate();
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
    }

    public void ClearCategory() => CategoryId = null;

    public void UpdateTags(IEnumerable<Tag> newTags)
    {
        if (newTags == null || !newTags.Any())
        {
            throw new DomainException("Cannot set null tags to a note");
        }

        if (newTags.Any(t => t.OwnerId != OwnerId))
        {
            throw new DomainException("Cannot set a tag of other owner to this note");
        }

        _tags.Clear();
        _tags.AddRange(newTags);
    }

    public void ClearTags() => _tags.Clear();

    public void SetPlainText(string? plainText) => PlainText = plainText;

    public void RegisterUpdate()
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException("Notas na lixeira não podem ser editadas. Restaure a nota primeiro.");
        }
    }
}
