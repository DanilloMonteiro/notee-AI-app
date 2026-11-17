namespace NoteLab.Domain;

/// <summary>Lente = outro ângulo sobre o tema; Crítica = lacuna no raciocínio da nota.</summary>
public enum PerspectiveKind
{
    Lens,
    Critique
}

public enum PerspectiveStatus
{
    Pending,
    Accepted,
    Dismissed
}

/// <summary>Categorias aceitas por tipo (proposta "Lentes e Crítica", fatia F1).</summary>
public static class PerspectiveCategories
{
    public const string Economic = "economic";
    public const string Political = "political";
    public const string Legal = "legal";
    public const string Historical = "historical";
    public const string Philosophical = "philosophical";
    public const string Social = "social";
    public const string Geometric = "geometric";
    public const string Physical = "physical";
    public const string Formal = "formal";

    public const string UnsupportedClaim = "unsupported-claim";
    public const string LogicalLeap = "logical-leap";
    public const string Generalization = "generalization";
    public const string OneSided = "one-sided";
    public const string AmbiguousTerm = "ambiguous-term";
    public const string CorrelationAsCausation = "correlation-as-causation";

    public static readonly IReadOnlySet<string> Lens = new HashSet<string>(StringComparer.Ordinal)
    {
        Economic, Political, Legal, Historical, Philosophical, Social, Geometric, Physical, Formal
    };

    public static readonly IReadOnlySet<string> Critique = new HashSet<string>(StringComparer.Ordinal)
    {
        UnsupportedClaim, LogicalLeap, Generalization, OneSided, AmbiguousTerm, CorrelationAsCausation
    };

    public static bool IsValid(PerspectiveKind kind, string category) =>
        kind == PerspectiveKind.Lens ? Lens.Contains(category) : Critique.Contains(category);
}

/// <summary>A nota mudou depois da geração: a sugestão precisa ser revalidada (HTTP 409 na API).</summary>
public sealed class StalePerspectiveException(string message) : Exception(message);

/// <summary>
/// Sugestão de lente ou crítica gerada a partir de uma versão da nota.
/// É sempre uma pergunta ("caminho para explorar"), nunca um veredicto ou fato.
/// Só vira <see cref="Doubt"/> com aceite individual e se a nota não mudou.
/// </summary>
public sealed class PerspectiveSuggestion
{
    public const int PromptMaxLength = 500;
    public const int DismissReasonMaxLength = 300;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public Guid NoteId { get; private set; }
    /// <summary>Versão da nota usada na geração; diferente da atual → estado "desatualizado".</summary>
    public long NoteVersion { get; private set; }
    public PerspectiveKind Kind { get; private set; }
    public string Category { get; private set; }
    public string Prompt { get; private set; }
    /// <summary>Obrigatória para críticas; nula para lentes.</summary>
    public DoubtAnchor? Anchor { get; private set; }
    public string Provider { get; private set; }
    public PerspectiveStatus Status { get; private set; }
    /// <summary>Visível só para o owner; não deve ir para logs ou telemetria.</summary>
    public string? DismissReason { get; private set; }
    public Guid? DoubtId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private PerspectiveSuggestion()
    {
        OwnerId = null!;
        Category = null!;
        Prompt = null!;
        Provider = null!;
    }

    public PerspectiveSuggestion(Note note, PerspectiveKind kind, string category, string prompt, DoubtAnchor? anchor, string provider)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Tipo de sugestão inválido.");
        }

        if (!PerspectiveCategories.IsValid(kind, category))
        {
            throw new DomainException("Categoria de sugestão inválida.");
        }

        string trimmed = (prompt ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > PromptMaxLength)
        {
            throw new DomainException($"A sugestão deve ter entre 1 e {PromptMaxLength} caracteres.");
        }

        if (!trimmed.EndsWith('?'))
        {
            throw new DomainException("Lentes e críticas devem ser formuladas como pergunta.");
        }

        if (kind == PerspectiveKind.Critique && anchor is null)
        {
            throw new DomainException("Críticas precisam estar ancoradas em um trecho da nota.");
        }

        if (kind == PerspectiveKind.Lens && anchor is not null)
        {
            throw new DomainException("Lentes se referem à nota inteira e não têm âncora.");
        }

        if (anchor is not null && (string.IsNullOrWhiteSpace(anchor.BlockId) || string.IsNullOrEmpty(anchor.ExactQuote)
            || anchor.StartIndex < 0 || anchor.EndIndex - anchor.StartIndex != anchor.ExactQuote.Length))
        {
            throw new DomainException("Âncora inválida.");
        }

        Id = Guid.NewGuid();
        OwnerId = note.OwnerId;
        NoteId = note.Id;
        NoteVersion = note.Version;
        Kind = kind;
        Category = category;
        Prompt = trimmed;
        Anchor = anchor;
        Provider = string.IsNullOrWhiteSpace(provider) ? "unknown" : provider;
        Status = PerspectiveStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsStale(long currentNoteVersion) => currentNoteVersion != NoteVersion;

    /// <summary>Aceite individual: cria a dúvida ligada à nota (ancorada, se for crítica).</summary>
    public Doubt Accept(long currentNoteVersion)
    {
        EnsurePending();
        if (IsStale(currentNoteVersion))
        {
            throw new StalePerspectiveException("A nota mudou desde que a sugestão foi gerada. Gere novamente.");
        }

        Doubt doubt = Doubt.FromPerspective(OwnerId, NoteId, NoteVersion, Anchor, Prompt);
        DoubtId = doubt.Id;
        Status = PerspectiveStatus.Accepted;
        DecidedAt = DateTimeOffset.UtcNow;
        return doubt;
    }

    public void Dismiss(string? reason)
    {
        EnsurePending();
        string? trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > DismissReasonMaxLength })
        {
            throw new DomainException($"O motivo deve ter no máximo {DismissReasonMaxLength} caracteres.");
        }

        DismissReason = trimmed;
        Status = PerspectiveStatus.Dismissed;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != PerspectiveStatus.Pending)
        {
            throw new DomainException("A sugestão já foi decidida.");
        }
    }
}
