namespace NoteLab.Application;

public sealed record NoteResponse(Guid Id, string Title, string Status);