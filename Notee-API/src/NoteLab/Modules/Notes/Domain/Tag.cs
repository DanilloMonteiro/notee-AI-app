namespace NoteLab.Domain;

public sealed class Tag
{
    public Guid Id { get; private set; }

    public string OwnerId { get; private set; }

    public string Name { get; private set; }

   public Tag(string ownerId, string name)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Name = name;
    }
}