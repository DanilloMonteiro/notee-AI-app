namespace NoteLab.Domain;

public enum NoteReferenceKind
{
    Book,
    Source
}

/// <summary>
/// Livro ou fonte registrado pelo autor em uma nota (ADR-016). É um metadado informado
/// pela pessoa — não é evidência verificada nem equivale a uma fonte de pesquisa.
/// Pertence à nota, portanto aparece igual em todos os mapas.
/// </summary>
public sealed class NoteReference
{
    public const int TitleMaxLength = 220;
    public const int PersonMaxLength = 140;
    public const int YearMaxLength = 15;
    public const int UrlMaxLength = 2048;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public Guid NoteId { get; private set; }
    public NoteReferenceKind Kind { get; private set; }
    public string Title { get; private set; }
    public string? Author { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublicationYear { get; private set; }
    public string? Url { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private NoteReference()
    {
        OwnerId = null!;
        Title = null!;
    }

    public NoteReference(
        string ownerId,
        Guid noteId,
        NoteReferenceKind kind,
        string title,
        string? author,
        string? publisher,
        string? publicationYear,
        string? url)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || noteId == Guid.Empty)
        {
            throw new DomainException("Nota e dono são obrigatórios.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Tipo de referência inválido.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        NoteId = noteId;
        Kind = kind;
        Title = Required(title, TitleMaxLength, "Título");
        // Livro usa autor/ano; fonte usa editora/instituição.
        Author = kind == NoteReferenceKind.Book ? Optional(author, PersonMaxLength, "Autor") : null;
        PublicationYear = kind == NoteReferenceKind.Book ? Optional(publicationYear, YearMaxLength, "Ano") : null;
        Publisher = kind == NoteReferenceKind.Source ? Optional(publisher, PersonMaxLength, "Editora/instituição") : null;
        Url = NormalizeUrl(url);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private static string Required(string? value, int max, string field)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > max)
        {
            throw new DomainException($"{field} é obrigatório e deve ter até {max} caracteres.");
        }

        return trimmed;
    }

    private static string? Optional(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw new DomainException($"{field} deve ter até {max} caracteres.");
        }

        return trimmed;
    }

    /// <summary>Aceita somente URLs absolutas http/https, evitando javascript:, data: etc.</summary>
    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string trimmed = url.Trim();
        if (trimmed.Length > UrlMaxLength
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("URL deve ser um endereço http(s) válido.");
        }

        return uri.ToString();
    }
}
