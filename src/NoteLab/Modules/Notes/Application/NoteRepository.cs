using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;
public class NoteRepository : INoteRepository
{

    private NoteDbContext _db; 

    public NoteRepository(NoteDbContext db)
    {
        this._db = db;
    }
    async public Task<List<NoteResponse>> GetNotesAsync(string ownerId, int page, int pageSize, CancellationToken ct)
    {

        List<NoteResponse> notes = await this._db.Notes.AsNoTracking()
            .Select(n => new NoteResponse(n.Id, n.Title.Value, n.Status.ToString()))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return notes;
    }
    
    public async Task<Note?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        Note note = await this._db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);

        return note;
    }

    async public Task<Note> AddNoteAsync(string ownerId, Note data, CancellationToken ct)
    {
        Note note = data;

        this._db.Notes.Add(note);
        await this._db.SaveChangesAsync(ct);

        return note;
    }

    public async Task<Note?> UpdateNoteAsync(string ownerId, long expectedVersion, Guid id, string title, string content, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return null;
        }
        
        Note note = await this._db.Notes.SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);

        if (note == null)
        {
            return null;
        }

        if (note.Version != expectedVersion) 
        {
            throw new DbUpdateConcurrencyException("Incompatibilidade de versão");
        }

        note.Update(new NoteTitle(title), content);

        await _db.SaveChangesAsync(ct);

        return note;
    }

    public async Task<bool> DeleteNoteAsync(string ownerId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return false;
        }
        
        Note note = await this.GetByIdAsync(ownerId, id, ct);

        if (note == null)
        {
            return false;
        }

        this._db.Notes.Remove(note);
        await _db.SaveChangesAsync(ct);

        return true;
    }
} 