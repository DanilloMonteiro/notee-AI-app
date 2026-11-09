namespace NoteLab.Application;

public record TagResponse(Guid Id, string Name);

public record CategoryResponse(Guid Id, string Name, int NoteCount);

public record TaxonomyItemResponse(Guid Id, string Name, int NoteCount);

public record NoteResponse(Guid Id, string Title, string Content, long Version, string Status, List<TagResponse> Tags)
{
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? TrashedAt { get; init; }
}

/// <summary>status: active (rascunho+ativa, padrão) | archived | trashed | all.</summary>
public sealed record NoteQuery(string? Status, string? Query, Guid? CategoryId, Guid? TagId, int Page, int PageSize);

public sealed record NotePage(List<NoteResponse> Items, int Total);

public sealed record RenameRequest(string Name);

public sealed record RevisionSummaryResponse(Guid Id, long Version, string Title, string Reason, long? RestoredFromVersion, DateTimeOffset CreatedAt);

public sealed record RevisionResponse(
    Guid Id,
    long Version,
    string Title,
    string Content,
    Guid? CategoryId,
    string? CategoryName,
    List<TagResponse> Tags,
    string Reason,
    long? RestoredFromVersion,
    DateTimeOffset CreatedAt);

public sealed record RestoreRevisionRequest(Guid RevisionId, long ExpectedVersion);
