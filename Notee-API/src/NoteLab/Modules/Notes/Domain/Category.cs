namespace NoteLab.Domain;

public sealed class Category
{
    public const int NameMaxLength = 60;

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public string Name { get; private set; }

    public Category(string ownerId, string name)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Name = name;
    }

    /// <summary>Cria com validação (o construtor fica sem regras para o EF materializar dados antigos).</summary>
    public static Category Create(string ownerId, string name) => new(ownerId, Validate(name));

    public void Rename(string name) => Name = Validate(name);

    internal static string Validate(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"O nome deve ter entre 1 e {NameMaxLength} caracteres.");
        }

        return trimmed;
    }
}
