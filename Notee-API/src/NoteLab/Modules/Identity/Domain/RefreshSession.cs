using System.Security.Cryptography;
using System.Text;

namespace NoteLab.Domain;

/// <summary>
/// Sessão de refresh token (identity.sessions no planejamento). Só o HASH do token é guardado.
/// Rotação a cada uso; reutilizar um token já usado revoga a família inteira (detecção de roubo).
/// </summary>
public sealed class RefreshSession
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    private RefreshSession()
    {
        UserId = null!;
        TokenHash = null!;
    }

    private RefreshSession(string userId, Guid familyId, string tokenHash, TimeSpan lifetime)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt + lifetime;
    }

    /// <summary>Cria sessão nova (nova família no login, mesma família na rotação) e devolve o token em claro uma única vez.</summary>
    public static (RefreshSession Session, string Token) Issue(string userId, Guid? familyId, TimeSpan lifetime)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (new RefreshSession(userId, familyId ?? Guid.NewGuid(), Hash(token), lifetime), token);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && UsedAt is null && ExpiresAt > now;

    public void MarkRotated(Guid replacementId, DateTimeOffset now)
    {
        UsedAt = now;
        ReplacedById = replacementId;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
