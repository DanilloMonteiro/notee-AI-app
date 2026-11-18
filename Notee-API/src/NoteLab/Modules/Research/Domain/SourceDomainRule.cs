namespace NoteLab.Domain;

public enum SourceDomainRuleKind
{
    Blocked,
    Trusted,
}

/// <summary>
/// Site que a pessoa não quer como fonte (<see cref="SourceDomainRuleKind.Blocked"/>) ou em que confia
/// (<see cref="SourceDomainRuleKind.Trusted"/>: entra primeiro e com nota mínima menor). ADR-047.
/// Um domínio por pessoa; vale também para os subdomínios.
/// </summary>
public sealed class SourceDomainRule
{
    public const int MaxDomainLength = 253;
    public const int MaxPerOwner = 200;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public string Domain { get; private set; }
    public SourceDomainRuleKind Kind { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private SourceDomainRule()
    {
        OwnerId = null!;
        Domain = null!;
    }

    /// <param name="domain">Já normalizado (sem esquema, caminho e "www.").</param>
    public SourceDomainRule(string ownerId, string domain, SourceDomainRuleKind kind)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(domain) || domain.Length > MaxDomainLength)
        {
            throw new DomainException("Site inválido.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Domain = domain;
        Kind = kind;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Change(SourceDomainRuleKind kind)
    {
        Kind = kind;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
