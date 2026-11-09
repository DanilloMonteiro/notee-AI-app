namespace NoteLab.Application;

// ---- Requisições ----------------------------------------------------------

public record CreateBoardRequest(string Title, string? Description);

public record UpdateBoardRequest(string Title, string? Description, long ExpectedVersion);

/// <summary>
/// ExpectedVersion nulo indica que o cliente ainda não conhece posição persistida
/// (cartão em posição padrão). Se já existir posição gravada, a API responde 409.
/// </summary>
public record MovePlacementRequest(double X, double Y, long? ExpectedVersion);

public record PlacementItemRequest(Guid NoteId, double X, double Y, long? ExpectedVersion);

public record BatchPlacementsRequest(List<PlacementItemRequest> Placements);

public record CreateEdgeRequest(Guid FromNoteId, Guid ToNoteId, string Type, string? Label);

public record UpdateEdgeRequest(Guid FromNoteId, Guid ToNoteId, string Type, string? Label, long ExpectedVersion);

// ---- Respostas ------------------------------------------------------------

public record BoardSummaryResponse(
    Guid Id,
    string Title,
    string? Description,
    long Version,
    DateTimeOffset UpdatedAt,
    int NoteCount,
    int EdgeCount);

/// <summary>PositionVersion nulo = posição padrão calculada, ainda não gravada.</summary>
public record BoardNodeResponse(
    Guid NoteId,
    string Title,
    string Excerpt,
    string Status,
    string? CategoryName,
    List<TagResponse> Tags,
    double X,
    double Y,
    long? PositionVersion,
    int ReferenceCount,
    int DoubtCount);

public record BoardEdgeResponse(
    Guid Id,
    Guid FromNoteId,
    Guid ToNoteId,
    string Type,
    string? Label,
    long Version);

public record BoardDoubtResponse(Guid Id, Guid NoteId, string Question, string Status);

public record BoardDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    long Version,
    DateTimeOffset UpdatedAt,
    List<BoardNodeResponse> Nodes,
    List<BoardEdgeResponse> Edges,
    List<BoardDoubtResponse> Doubts);

public record PlacementResponse(Guid NoteId, double X, double Y, long PositionVersion);

// ---- Resultado de casos de uso -------------------------------------------

public enum MapErrorKind
{
    NotFound,
    Conflict,
    Validation,
    TooManyRequests
}

public sealed record MapResult<T>(T? Value, MapErrorKind? Error = null, string? Message = null)
{
    public bool IsSuccess => Error is null;

    public static MapResult<T> Ok(T value) => new(value);
    public static MapResult<T> NotFound(string message) => new(default, MapErrorKind.NotFound, message);
    public static MapResult<T> Conflict(string message) => new(default, MapErrorKind.Conflict, message);
    public static MapResult<T> Invalid(string message) => new(default, MapErrorKind.Validation, message);
    public static MapResult<T> TooMany(string message) => new(default, MapErrorKind.TooManyRequests, message);
}
