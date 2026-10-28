namespace NoteLab.Application;

public sealed record CreateNoteRequest(string Title, string? Content);