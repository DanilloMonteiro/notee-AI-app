namespace NoteLab.Domain;

public sealed record NoteTitle
{
    public string Value { get;}

    public NoteTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new ArgumentException("Note title cannot be null or bigger then 100 caracters");
        }

        this.Value = value;
    }
}