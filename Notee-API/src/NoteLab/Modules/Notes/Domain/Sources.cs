using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoteLab.Domain;

public enum SourceKind { Book, Article, Video, Study, Dataset, Document, Website, Person, Interview, Other }
public enum SourceLocatorKind { Page, Pages, Timestamp, Section, Paragraph, None }

/// <summary>Metadados bibliográficos privados, reutilizáveis pelo dono.</summary>
public sealed class Source
{
    public const int TitleMaxLength = 300;
    public const int AuthorMaxLength = 140;
    public const int AuthorsMaxCount = 20;
    public const int VenueMaxLength = 300;
    public const int LanguageMaxLength = 32;
    public const int DoiMaxLength = 256;
    public const int IsbnMaxLength = 13;
    public const int UrlMaxLength = 2048;
    public const int LicenseMaxLength = 64;
    public const int NotesMaxLength = 2000;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = null!;
    public SourceKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public string AuthorsJson { get; private set; } = "[]";
    public int? Year { get; private set; }
    public string? Venue { get; private set; }
    public string? Language { get; private set; }
    public string? Doi { get; private set; }
    public string? Isbn { get; private set; }
    public string? Url { get; private set; }
    public string License { get; private set; } = "unknown";
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Source() { }

    public Source(string ownerId, SourceKind kind, string title, IEnumerable<string>? authors, int? year,
        string? venue, string? language, string? doi, string? isbn, string? url, string? license, string? notes)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new DomainException("Dono é obrigatório.");
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
        Set(kind, title, authors, year, venue, language, doi, isbn, url, license, notes);
    }

    public IReadOnlyList<string> Authors => JsonSerializer.Deserialize<List<string>>(AuthorsJson) ?? [];

    public void Update(SourceKind kind, string title, IEnumerable<string>? authors, int? year,
        string? venue, string? language, string? doi, string? isbn, string? url, string? license, string? notes)
    {
        Set(kind, title, authors, year, venue, language, doi, isbn, url, license, notes);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Set(SourceKind kind, string title, IEnumerable<string>? authors, int? year,
        string? venue, string? language, string? doi, string? isbn, string? url, string? license, string? notes)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("Tipo de fonte inválido.");
        Title = Required(title, TitleMaxLength, "Título");
        List<string> normalizedAuthors = (authors ?? []).Select(a => (a ?? "").Trim()).Where(a => a.Length > 0).ToList();
        if (normalizedAuthors.Count > AuthorsMaxCount || normalizedAuthors.Any(a => a.Length > AuthorMaxLength))
            throw new DomainException($"Informe até {AuthorsMaxCount} autores, com até {AuthorMaxLength} caracteres cada.");
        AuthorsJson = JsonSerializer.Serialize(normalizedAuthors);
        if (year is < 1000 or > 9999) throw new DomainException("Ano deve ter quatro dígitos.");
        Year = year;
        Kind = kind;
        Venue = Optional(venue, VenueMaxLength, "Editora, periódico ou canal");
        Language = Optional(language, LanguageMaxLength, "Idioma");
        Doi = NormalizeDoi(doi);
        Isbn = NormalizeIsbn(isbn);
        Url = NormalizeUrl(url);
        License = NormalizeLicense(license);
        Notes = Optional(notes, NotesMaxLength, "Notas");
    }

    public static string? NormalizeDoi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string doi = value.Trim();
        doi = Regex.Replace(doi, @"^https?://(dx\.)?doi\.org/", "", RegexOptions.IgnoreCase);
        doi = Regex.Replace(doi, @"^doi:\s*", "", RegexOptions.IgnoreCase).Trim();
        if (doi.Length > DoiMaxLength || !Regex.IsMatch(doi, @"^10\.\d{4,9}/\S+$", RegexOptions.IgnoreCase))
            throw new DomainException("DOI inválido.");
        return doi.ToLowerInvariant();
    }

    public static string? NormalizeIsbn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string isbn = Regex.Replace(value, @"[\s-]", "").ToUpperInvariant();
        if (isbn.Length == 10 && Regex.IsMatch(isbn, @"^\d{9}[\dX]$"))
        {
            int sum = 0;
            for (int i = 0; i < 10; i++) sum += (isbn[i] == 'X' ? 10 : isbn[i] - '0') * (10 - i);
            if (sum % 11 != 0) throw new DomainException("ISBN-10 inválido (dígito verificador incorreto).");
            string firstTwelve = "978" + isbn[..9];
            int check = Isbn13CheckDigit(firstTwelve);
            isbn = firstTwelve + check;
        }
        else if (isbn.Length == 13 && Regex.IsMatch(isbn, @"^\d{13}$"))
        {
            if (Isbn13CheckDigit(isbn[..12]) != isbn[12] - '0')
                throw new DomainException("ISBN-13 inválido (dígito verificador incorreto).");
        }
        else throw new DomainException("ISBN deve ser ISBN-10 ou ISBN-13 válido.");
        return isbn;
    }

    private static int Isbn13CheckDigit(string firstTwelve)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++) sum += (firstTwelve[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    private static string NormalizeLicense(string? value)
    {
        string license = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
        if (license is "unknown" or "public-domain" or "open-access" or "proprietary") return license;
        if (license.Length <= LicenseMaxLength && Regex.IsMatch(license, @"^cc-[a-z0-9-]{1,56}$")) return license;
        throw new DomainException("Licença deve ser unknown, public-domain, open-access, proprietary ou cc-*.");
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string url = value.Trim();
        if (url.Length > UrlMaxLength || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new DomainException("URL deve ser http(s) válida e não pode incluir credenciais.");
        return uri.ToString();
    }

    private static string Required(string? value, int max, string field)
    {
        string text = (value ?? "").Trim();
        if (text.Length is < 1 || text.Length > max) throw new DomainException($"{field} é obrigatório e deve ter até {max} caracteres.");
        return text;
    }

    private static string? Optional(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value.Trim();
        if (text.Length > max) throw new DomainException($"{field} deve ter até {max} caracteres.");
        return text;
    }
}

/// <summary>Citação privada de uma fonte em uma nota, com âncora e localização bibliográfica opcionais.</summary>
public sealed class SourceCitation
{
    public const int BlockIdMaxLength = 100;
    public const int ExactQuoteMaxLength = 500;
    public const int LocatorTextMaxLength = 200;
    public const int SourceQuoteMaxLength = 2000;
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = null!;
    public Guid NoteId { get; private set; }
    public Guid SourceId { get; private set; }
    public string? BlockId { get; private set; }
    public string? ExactQuote { get; private set; }
    public int? StartIndex { get; private set; }
    public int? EndIndex { get; private set; }
    public SourceLocatorKind LocatorKind { get; private set; }
    public string? LocatorText { get; private set; }
    public int? PageStart { get; private set; }
    public int? PageEnd { get; private set; }
    public int? TimeStartSec { get; private set; }
    public int? TimeEndSec { get; private set; }
    public string? SourceQuote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private SourceCitation() { }

    public SourceCitation(string ownerId, Guid noteId, Guid sourceId, string? blockId, string? exactQuote,
        int? startIndex, int? endIndex, SourceLocatorKind locatorKind, string? locatorText,
        int? pageStart, int? pageEnd, int? timeStartSec, int? timeEndSec, string? sourceQuote)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || noteId == Guid.Empty || sourceId == Guid.Empty)
            throw new DomainException("Nota, fonte e dono são obrigatórios.");
        Id = Guid.NewGuid(); OwnerId = ownerId; NoteId = noteId; SourceId = sourceId; CreatedAt = DateTimeOffset.UtcNow;
        Update(blockId, exactQuote, startIndex, endIndex, locatorKind, locatorText, pageStart, pageEnd, timeStartSec, timeEndSec, sourceQuote);
    }

    public void Update(string? blockId, string? exactQuote, int? startIndex, int? endIndex,
        SourceLocatorKind locatorKind, string? locatorText, int? pageStart, int? pageEnd,
        int? timeStartSec, int? timeEndSec, string? sourceQuote)
    {
        bool hasAnchor = !string.IsNullOrWhiteSpace(blockId) || !string.IsNullOrWhiteSpace(exactQuote) || startIndex.HasValue || endIndex.HasValue;
        if (hasAnchor && (string.IsNullOrWhiteSpace(blockId) || blockId.Length > BlockIdMaxLength ||
            string.IsNullOrEmpty(exactQuote) || exactQuote.Length > ExactQuoteMaxLength || !startIndex.HasValue ||
            startIndex < 0 || endIndex != startIndex + exactQuote.Length))
            throw new DomainException("Âncora deve conter bloco, trecho e índices consistentes.");
        if (!Enum.IsDefined(locatorKind)) throw new DomainException("Tipo de localização inválido.");
        if (pageStart is < 1 || pageEnd is < 1 || (pageStart.HasValue && pageEnd.HasValue && pageEnd < pageStart))
            throw new DomainException("Páginas devem ser positivas e o fim não pode anteceder o início.");
        if (timeStartSec is < 0 || timeEndSec is < 0 || (timeStartSec.HasValue && timeEndSec.HasValue && timeEndSec < timeStartSec))
            throw new DomainException("Tempos devem ser positivos e o fim não pode anteceder o início.");
        if (locatorKind == SourceLocatorKind.Page && !pageStart.HasValue) throw new DomainException("Informe a página.");
        if (locatorKind == SourceLocatorKind.Pages && !pageStart.HasValue) throw new DomainException("Informe o intervalo de páginas.");
        if (locatorKind == SourceLocatorKind.Timestamp && !timeStartSec.HasValue) throw new DomainException("Informe o minuto/segundo.");
        BlockId = hasAnchor ? blockId!.Trim() : null;
        ExactQuote = hasAnchor ? exactQuote : null;
        StartIndex = hasAnchor ? startIndex : null;
        EndIndex = hasAnchor ? endIndex : null;
        LocatorKind = locatorKind;
        LocatorText = Optional(locatorText, LocatorTextMaxLength, "Localização");
        PageStart = pageStart; PageEnd = pageEnd; TimeStartSec = timeStartSec; TimeEndSec = timeEndSec;
        SourceQuote = Optional(sourceQuote, SourceQuoteMaxLength, "Trecho da fonte");
    }

    public void ReassignSource(Guid sourceId)
    {
        if (sourceId == Guid.Empty) throw new DomainException("Fonte inválida.");
        SourceId = sourceId;
    }

    private static string? Optional(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value.Trim();
        if (text.Length > max) throw new DomainException($"{field} deve ter até {max} caracteres.");
        return text;
    }
}
