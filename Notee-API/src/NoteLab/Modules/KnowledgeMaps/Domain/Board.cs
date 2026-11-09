namespace NoteLab.Domain;

/// <summary>
/// Mapa de ideias (módulo KnowledgeMaps). Guarda apenas metadados do quadro;
/// o conteúdo das notas continua no módulo Notes (ADR-011).
/// </summary>
public sealed class Board
{
    public const int TitleMaxLength = 80;
    public const int DescriptionMaxLength = 240;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Board()
    {
        OwnerId = null!;
        Title = null!;
    }

    public Board(string ownerId, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new DomainException("Board owner is required");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        (Title, Description) = Validate(title, description);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateDetails(string title, string? description)
    {
        (Title, Description) = Validate(title, description);
        Version++;
        Touch();
    }

    /// <summary>Marca atividade no quadro (posição/conexão) sem alterar a versão dos metadados.</summary>
    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static (string Title, string? Description) Validate(string title, string? description)
    {
        string trimmedTitle = (title ?? string.Empty).Trim();
        if (trimmedTitle.Length == 0 || trimmedTitle.Length > TitleMaxLength)
        {
            throw new DomainException($"O nome do mapa é obrigatório e deve ter até {TitleMaxLength} caracteres.");
        }

        string? trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            throw new DomainException($"A descrição deve ter até {DescriptionMaxLength} caracteres.");
        }

        return (trimmedTitle, trimmedDescription);
    }
}
