namespace NoteLab.Domain;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; }
    public string Name { get; private set; }

    public Category(string ownerId, string name)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Name = name;
    }
}