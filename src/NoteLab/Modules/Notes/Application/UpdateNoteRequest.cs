namespace NoteLab.Application;

public sealed record UpdateNoteRequest(string Title, long ExpectedVersion, string? Content, Guid? CategoryId, List<Guid>? TagIds);