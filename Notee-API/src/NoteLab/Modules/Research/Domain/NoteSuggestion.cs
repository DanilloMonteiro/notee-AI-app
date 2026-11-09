namespace NoteLab.Domain;

public enum NoteSuggestionKind
{
    Tag,
    Category,
    Doubt
}

public enum NoteSuggestionStatus
{
    Pending,
    Accepted,
    Dismissed,
    Stale
}

/// <summary>
/// Sugestão de organização gerada pela análise de uma versão da nota (M10).
/// Nunca altera a nota sozinha: aplicar exige aceite e a nota não pode ter mudado.
/// </summary>
public sealed class NoteSuggestion
{
    public const int ValueMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid NoteId { get; private set; }
    public string OwnerId { get; private set; }
    public long NoteVersion { get; private set; }
    public NoteSuggestionKind Kind { get; private set; }
    public string Value { get; private set; }
    public string Reason { get; private set; }
    public string Provider { get; private set; }
    public NoteSuggestionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private NoteSuggestion()
    {
        OwnerId = null!;
        Value = null!;
        Reason = null!;
        Provider = null!;
    }

    public NoteSuggestion(Note note, NoteSuggestionKind kind, string value, string reason, string provider)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > ValueMaxLength)
        {
            throw new DomainException("Valor de sugestão inválido.");
        }

        Id = Guid.NewGuid();
        NoteId = note.Id;
        OwnerId = note.OwnerId;
        NoteVersion = note.Version;
        Kind = kind;
        Value = trimmed;
        Reason = reason.Length > 300 ? reason[..300] : reason;
        Provider = provider;
        Status = NoteSuggestionStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Decide(NoteSuggestionStatus status)
    {
        if (Status != NoteSuggestionStatus.Pending)
        {
            throw new DomainException("A sugestão já foi decidida.");
        }

        if (status == NoteSuggestionStatus.Pending)
        {
            throw new DomainException("Decisão inválida.");
        }

        Status = status;
        DecidedAt = DateTimeOffset.UtcNow;
    }
}

public enum AutoResearchOverride
{
    Inherit,
    On,
    Off
}

/// <summary>Configuração por nota que sobrepõe a da conta (precedência nota > conta).</summary>
public sealed class NoteSettings
{
    public Guid NoteId { get; private set; }
    public string OwnerId { get; private set; }
    public AutoResearchOverride AutoResearch { get; private set; }
    public long Version { get; private set; } = 1;

    private NoteSettings()
    {
        OwnerId = null!;
    }

    public NoteSettings(Guid noteId, string ownerId)
    {
        NoteId = noteId;
        OwnerId = ownerId;
        AutoResearch = AutoResearchOverride.Inherit;
    }

    public void SetAutoResearch(AutoResearchOverride value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainException("Valor inválido.");
        }

        AutoResearch = value;
        Version++;
    }

    /// <summary>Regra de precedência: pesquisa desligada na conta vence tudo; depois a nota; depois a conta.</summary>
    public static bool ShouldAutoResearch(UserPreferences prefs, AutoResearchOverride noteOverride) =>
        prefs.ResearchEnabled && noteOverride switch
        {
            AutoResearchOverride.On => true,
            AutoResearchOverride.Off => false,
            _ => prefs.AutoResearch,
        };
}
