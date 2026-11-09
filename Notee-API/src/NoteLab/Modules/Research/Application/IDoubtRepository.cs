using NoteLab.Domain;

namespace NoteLab.Application;
public interface IDoubtRepository
{
    public Task<Doubt> AddDoubtAsync(Doubt doubt, CancellationToken ct);

    public Task<Doubt?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct);

    public Task<List<Doubt>> GetByNoteIdAsync(string ownerId, Guid noteId, CancellationToken ct);

    public Task<List<Doubt>> GetAllAsync(string ownerId, CancellationToken ct);

    public Task<bool> DeleteDoubtAsync(string ownerId, Guid id, CancellationToken ct);
}