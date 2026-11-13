namespace NoteLab.Domain;

public sealed class NoteComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string OwnerId { get; set; } = "";
    public string BlockId { get; set; } = "";
    public string ExactQuote { get; set; } = "";
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
