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

     public async Task<List<Doubt>> GetAllAsync(string ownerId, CancellationToken ct)
    {
        return await _db.Doubts.AsNoTracking().Where(d => d.OwnerId == ownerId).OrderByDescending(d => d.CreatedAt).ToListAsync();
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

    public async Task<List<Doubt>> GetByNoteIdAsync(string ownerId, Guid noteId, CancellationToken ct)
    {

    
        if (noteId == Guid.Empty)
        {
            return [];
        }    

        return await _db.Doubts.AsNoTracking().Where(d => d.OwnerId == ownerId && d.NoteId == noteId).ToListAsync();
    }
}