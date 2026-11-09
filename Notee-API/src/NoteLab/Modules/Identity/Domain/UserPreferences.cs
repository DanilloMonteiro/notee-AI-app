namespace NoteLab.Domain;

/// <summary>
/// Preferências e consentimentos da conta (identity.preferences no planejamento).
/// Por padrão: pesquisa ligada, mas SEM consentimento para enviar dados a provedor externo.
/// </summary>
public sealed class UserPreferences
{
    /// <summary>Versão do texto de consentimento apresentado ao usuário.</summary>
    public const int CurrentConsentVersion = 1;

    public string UserId { get; private set; }
    public bool ResearchEnabled { get; private set; } = true;
    /// <summary>Pesquisar automaticamente ao criar dúvida (padrão da conta; a nota pode sobrepor).</summary>
    public bool AutoResearch { get; private set; } = true;
    public bool ExternalAiConsent { get; private set; }
    public int? ConsentVersion { get; private set; }
    public DateTimeOffset? ConsentedAt { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserPreferences()
    {
        UserId = null!;
    }

    public UserPreferences(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("Usuário é obrigatório.");
        }

        UserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static UserPreferences Default(string userId) => new(userId);

    public void SetAutoResearch(bool enabled)
    {
        AutoResearch = enabled;
        Touch();
    }

    public void SetResearchEnabled(bool enabled)
    {
        ResearchEnabled = enabled;
        Touch();
    }

    /// <summary>Conceder exige aceitar a versão vigente do termo; revogar vale imediatamente.</summary>
    public void SetExternalAiConsent(bool consent, int? acceptedVersion)
    {
        if (consent)
        {
            if (acceptedVersion != CurrentConsentVersion)
            {
                throw new DomainException("Aceite a versão atual do termo de uso de IA externa.");
            }

            ExternalAiConsent = true;
            ConsentVersion = CurrentConsentVersion;
            ConsentedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            ExternalAiConsent = false;
            ConsentedAt = null;
            ConsentVersion = null;
        }

        Touch();
    }

    /// <summary>Envio externo só é permitido com pesquisa ligada e consentimento na versão vigente.</summary>
    public bool AllowsExternalAi =>
        ResearchEnabled && ExternalAiConsent && ConsentVersion == CurrentConsentVersion;

    private void Touch()
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
