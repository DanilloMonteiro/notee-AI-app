using NoteLab.Domain;

namespace NoteLab.Application;

public interface INoteRepository
{
    public Task<NotePage> GetNotesAsync(string ownerId, NoteQuery query, CancellationToken ct);

    public Task<Note> AddNoteAsync(string ownerId, Note data, CancellationToken ct);

    public Task<Note?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct);

    public Task<NoteResponse?> GetResponseAsync(string ownerId, Guid id, CancellationToken ct);

    public Task<Note?> UpdateNoteAsync(string ownerId, long expectedVersion, Guid id, string title, string content, Guid? categoryId, List<Guid>? tagsId, CancellationToken ct);

    /// <summary>Aplica uma transição de ciclo de vida (archive/trash/restore) com versão esperada opcional.</summary>
    public Task<Note?> ChangeLifecycleAsync(string ownerId, Guid id, Action<Note> transition, long? expectedVersion, CancellationToken ct);

    /// <summary>Exclusão definitiva: só para notas na lixeira.</summary>
    public Task<DeleteResult> DeleteNoteAsync(string ownerId, Guid id, CancellationToken ct);
}

public enum DeleteResult
{
    Deleted,
    NotFound,
    NotInTrash
}
