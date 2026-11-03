using NoteLab.Domain;

namespace NoteLab.Application;
public interface IDoubtRepository
{
    public Task<Doubt> AddDoubtAsync(Doubt doubt, CancellationToken ct);

    public Task<Doubt?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct);
}