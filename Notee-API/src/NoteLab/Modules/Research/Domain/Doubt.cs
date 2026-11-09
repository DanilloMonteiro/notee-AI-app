namespace NoteLab.Domain;

public enum DoubtOrigin
{
    /// <summary>Trecho selecionado no editor (com âncora).</summary>
    Selection,
    /// <summary>Pergunta livre criada no mapa, sem âncora (ADR-013).</summary>
    BoardQuestion
}

/// <summary>Qualidade da evidência da resposta. Providers atuais não retornam citações verificáveis.</summary>
public enum AnswerEvidence
{
    NoSource,
    Sourced
}

public sealed class Doubt
{
    public const int QuestionMaxLength = 500;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public Guid NoteId { get; private set; }
    public long NoteRevision { get; private set; }
    /// <summary>Nulo para dúvidas do mapa (origem BoardQuestion).</summary>
    public DoubtAnchor? Anchor { get; private set; }
    public DoubtOrigin Origin { get; private set; }
    public string Question { get; private set; }
    public string? Answer { get; private set; }
    public string? AnswerProvider { get; private set; }
    public AnswerEvidence? Evidence { get; private set; }
    public DoubtStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Doubt()
    {
        OwnerId = null!;
        Question = null!;
    }

    public Doubt(string ownerId, Guid noteId, long noteRevision, DoubtAnchor anchor, string question)
        : this(ownerId, noteId, noteRevision, anchor ?? throw new DomainException("Âncora é obrigatória para dúvidas do editor."), question, DoubtOrigin.Selection)
    {
    }

    private Doubt(string ownerId, Guid noteId, long noteRevision, DoubtAnchor? anchor, string question, DoubtOrigin origin)
    {
        string trimmed = (question ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > QuestionMaxLength)
        {
            throw new DomainException($"A pergunta deve ter entre 1 e {QuestionMaxLength} caracteres.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        NoteId = noteId;
        NoteRevision = noteRevision;
        Anchor = anchor;
        Origin = origin;
        Question = trimmed;
        Status = DoubtStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Dúvida livre do mapa: ligada à nota, sem âncora inventada.</summary>
    public static Doubt FromBoard(string ownerId, Guid noteId, long noteRevision, string question) =>
        new(ownerId, noteId, noteRevision, null, question, DoubtOrigin.BoardQuestion);

    public bool IsFinal => Status is DoubtStatus.Completed or DoubtStatus.Failed or DoubtStatus.Cancelled;

    public void MarkQueued()
    {
        Status = DoubtStatus.Pending;
        Touch();
    }

    public void MarkAsRunning()
    {
        Status = DoubtStatus.Running;
        Touch();
    }

    public void Complete(string answer, string provider = "unknown", AnswerEvidence evidence = AnswerEvidence.NoSource)
    {
        Answer = answer;
        AnswerProvider = provider;
        Evidence = evidence;
        Status = DoubtStatus.Completed;
        Touch();
    }

    public void Fail()
    {
        Status = DoubtStatus.Failed;
        Touch();
    }

    public void Cancel()
    {
        Status = DoubtStatus.Cancelled;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
