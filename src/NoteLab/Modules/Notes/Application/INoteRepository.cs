using System.Security.Claims;
using NoteLab.Domain;

namespace NoteLab.Application;
public interface INoteRepository
{
    public Task<List<NoteResponse>> GetNotesAsync(string ownerId, int page, int pageSize, CancellationToken ct);

    public Task<Note> AddNoteAsync(string ownerId, Note data, CancellationToken ct);

    public Task<Note?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct);

    public Task<Note?> UpdateNoteAsync(string ownerId, long expectedVersion, Guid id, string title, string content, CancellationToken ct);

    public Task<bool> DeleteNoteAsync(string ownerId, Guid id, CancellationToken ct);
}