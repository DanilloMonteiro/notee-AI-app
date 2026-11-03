using NoteLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace NoteLab.Application;
public class DoubtRepository : IDoubtRepository
{
    private NoteDbContext _db; 

    public DoubtRepository(NoteDbContext db)
    {
        this._db = db;
    }
    public async Task<Doubt> AddDoubtAsync(Doubt doubt, CancellationToken ct)
    {
        _db.Doubts.Add(doubt);
        await _db.SaveChangesAsync(ct);
        return doubt;
    }

    public async Task<Doubt?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        Doubt? doubt = await this._db.Doubts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId, ct);
        
        return doubt;
    }
}