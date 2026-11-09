using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteLab.Domain;

public enum PublicationVisibility
{
    /// <summary>Só o autor vê a prévia.</summary>
    Private,
    /// <summary>Qualquer pessoa com o link secreto (token).</summary>
    Unlisted,
    /// <summary>Aparece no perfil e na biblioteca pública.</summary>
    Public
}

public enum PublicationStatus
{
    Published,
    Revoked
}

/// <summary>
/// Publicação de uma nota como SNAPSHOT revisado (M08). Editar a nota não altera o que foi publicado;
/// o autor publica explicitamente uma nova revisão. Revogar tira do ar imediatamente.
/// </summary>
public sealed partial class Publication
{
    public const int SlugMaxLength = 80;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public Guid NoteId { get; private set; }
    public string Slug { get; private set; }
    public string ShareToken { get; private set; }
    public PublicationVisibility Visibility { get; private set; }
    public PublicationStatus Status { get; private set; }
    public Guid? CurrentRevisionId { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private Publication()
    {
        OwnerId = null!;
        Slug = null!;
        ShareToken = null!;
    }

    public Publication(string ownerId, Guid noteId, string slug, PublicationVisibility visibility)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || noteId == Guid.Empty)
        {
            throw new DomainException("Dono e nota são obrigatórios.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        NoteId = noteId;
        Slug = NormalizeSlug(slug);
        ShareToken = NewToken();
        Visibility = Enum.IsDefined(visibility) ? visibility : throw new DomainException("Visibilidade inválida.");
        Status = PublicationStatus.Published;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public bool IsLive => Status == PublicationStatus.Published && CurrentRevisionId.HasValue;

    public void SetRevision(Guid revisionId)
    {
        EnsureNotRevoked();
        CurrentRevisionId = revisionId;
        Touch();
    }

    public void ChangeVisibility(PublicationVisibility visibility)
    {
        EnsureNotRevoked();
        if (!Enum.IsDefined(visibility))
        {
            throw new DomainException("Visibilidade inválida.");
        }

        Visibility = visibility;
        Touch();
    }

    /// <summary>Gera um novo link secreto, invalidando o anterior.</summary>
    public void RotateToken()
    {
        EnsureNotRevoked();
        ShareToken = NewToken();
        Touch();
    }

    public void Revoke()
    {
        EnsureNotRevoked();
        Status = PublicationStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Slug em minúsculas, sem acentos, só [a-z0-9-].</summary>
    public static string NormalizeSlug(string? value)
    {
        string text = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        string slug = SlugInvalid().Replace(sb.ToString(), "-").Trim('-');
        slug = SlugDashes().Replace(slug, "-");
        if (slug.Length == 0)
        {
            throw new DomainException("Informe um endereço (slug) com letras ou números.");
        }

        return slug.Length > SlugMaxLength ? slug[..SlugMaxLength].Trim('-') : slug;
    }

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private void EnsureNotRevoked()
    {
        if (Status == PublicationStatus.Revoked)
        {
            throw new DomainException("A publicação foi revogada.");
        }
    }

    private void Touch()
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugInvalid();

    [GeneratedRegex("-{2,}")]
    private static partial Regex SlugDashes();
}

/// <summary>Snapshot imutável do que foi publicado.</summary>
public sealed class PublicationRevision
{
    public Guid Id { get; private set; }
    public Guid PublicationId { get; private set; }
    public long NoteVersion { get; private set; }
    public string Title { get; private set; }
    public string? Content { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    private PublicationRevision()
    {
        Title = null!;
    }

    public PublicationRevision(Guid publicationId, Note note)
    {
        Id = Guid.NewGuid();
        PublicationId = publicationId;
        NoteVersion = note.Version;
        Title = note.Title.Value;
        Content = note.Content;
        PublishedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>Perfil público mínimo (handle) — sem rede social: só leitura de publicações.</summary>
public sealed partial class PublicProfile
{
    public const int HandleMaxLength = 30;
    public const int DisplayNameMaxLength = 60;

    public string UserId { get; private set; }
    public string Handle { get; private set; }
    public string DisplayName { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PublicProfile()
    {
        UserId = null!;
        Handle = null!;
        DisplayName = null!;
    }

    public PublicProfile(string userId, string handle, string displayName)
    {
        UserId = userId;
        Handle = null!;
        DisplayName = null!;
        Update(handle, displayName);
    }

    public void Update(string handle, string displayName)
    {
        string h = (handle ?? string.Empty).Trim().ToLowerInvariant();
        if (!HandlePattern().IsMatch(h))
        {
            throw new DomainException($"O identificador deve ter 3 a {HandleMaxLength} caracteres: letras minúsculas, números, '_' ou '-'.");
        }

        string name = (displayName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > DisplayNameMaxLength)
        {
            throw new DomainException($"O nome público deve ter entre 1 e {DisplayNameMaxLength} caracteres.");
        }

        Handle = h;
        DisplayName = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    [GeneratedRegex("^[a-z0-9_-]{3,30}$")]
    private static partial Regex HandlePattern();
}
