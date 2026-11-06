namespace NoteLab.Application;

public record TagResponse(Guid Id, string Name);

public record NoteResponse(Guid Id, string Title, long Version, string Status, List<TagResponse> Tags);