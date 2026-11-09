using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public class NoteRepository : INoteRepository
{
    private const int PlainTextMaxLength = 100_000;
    private readonly NoteDbContext _db;

    public NoteRepository(NoteDbContext db)
    {
        _db = db;
    }

    public async Task<NotePage> GetNotesAsync(string ownerId, NoteQuery query, CancellationToken ct)
    {
        IQueryable<Note> notes = _db.Notes;

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            // Busca textual no servidor (título + texto simples), sem diferenciar maiúsculas.
            string pattern = "%" + EscapeLike(query.Query.Trim()) + "%";
            notes = _db.Notes.FromSqlInterpolated(
                $"SELECT * FROM \"Notes\" WHERE \"Title\" ILIKE {pattern} OR COALESCE(\"PlainText\", \"Content\", '') ILIKE {pattern}");
        }

        notes = notes.AsNoTracking().Where(n => n.OwnerId == ownerId);

        notes = (query.Status ?? "active").ToLowerInvariant() switch
        {
            "archived" => notes.Where(n => n.Status == NoteStatus.Archived),
            "trashed" => notes.Where(n => n.Status == NoteStatus.Trashed),
            "all" => notes,
            _ => notes.Where(n => n.Status == NoteStatus.Draft || n.Status == NoteStatus.Active),
        };

        if (query.CategoryId.HasValue)
        {
            notes = notes.Where(n => n.CategoryId == query.CategoryId);
        }

        if (query.TagId.HasValue)
        {
            notes = notes.Where(n => n.Tags.Any(t => t.Id == query.TagId));
        }

        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        int page = Math.Max(query.Page, 1);

        int total = await notes.CountAsync(ct);
        List<Note> items = await notes
            .Include(n => n.Tags)
            .OrderByDescending(n => n.UpdatedAt).ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        Dictionary<Guid, string> categories = await CategoryNamesAsync(ownerId, items, ct);
        return new NotePage(items.Select(n => ToResponse(n, categories)).ToList(), total);
    }

    public async Task<Note?> GetByIdAsync(string ownerId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.Notes.AsNoTracking().Include(n => n.Tags)
            .SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
    }

    public async Task<NoteResponse?> GetResponseAsync(string ownerId, Guid id, CancellationToken ct)
    {
        Note? note = await GetByIdAsync(ownerId, id, ct);
        return note is null ? null : await ToResponseAsync(ownerId, note, ct);
    }

    public async Task<Note> AddNoteAsync(string ownerId, Note data, CancellationToken ct)
    {
        data.SetPlainText(NoteText.Extract(data.Content, PlainTextMaxLength));
        _db.Notes.Add(data);
        _db.NoteRevisions.Add(NoteRevision.Capture(data, RevisionReason.Created));
        await _db.SaveChangesAsync(ct);
        return data;
    }

    public async Task<Note?> UpdateNoteAsync(string ownerId, long expectedVersion, Guid id, string title, string content, Guid? categoryId, List<Guid>? tagIds, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        Note? note = await _db.Notes.Include(n => n.Tags).SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
        if (note == null)
        {
            return null;
        }

        if (note.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("Incompatibilidade de versão");
        }

        if (!note.IsEditable)
        {
            throw new DomainException("Notas na lixeira não podem ser editadas. Restaure a nota primeiro.");
        }

        // Notas criadas antes do histórico: guarda o estado anterior como linha de base.
        if (!await _db.NoteRevisions.AnyAsync(r => r.NoteId == note.Id, ct))
        {
            _db.NoteRevisions.Add(NoteRevision.CaptureBaseline(note));
        }

        // PUT substitui o estado: categoria nula remove a categoria.
        if (categoryId.HasValue)
        {
            Category category = await _db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId && c.OwnerId == ownerId, ct)
                                ?? throw new DomainException("Categoria não encontrada.");
            note.SetCategory(category);
        }
        else
        {
            note.ClearCategory();
        }

        // Tags: null mantém; lista vazia remove todas.
        if (tagIds != null)
        {
            List<Guid> distinct = tagIds.Distinct().ToList();
            if (distinct.Count == 0)
            {
                note.ClearTags();
            }
            else
            {
                List<Tag> tags = await _db.Tags.Where(t => distinct.Contains(t.Id) && t.OwnerId == ownerId).ToListAsync(ct);
                if (tags.Count != distinct.Count)
                {
                    throw new DomainException("Uma ou mais tags não foram encontradas.");
                }

                note.UpdateTags(tags);
            }
        }

        note.Update(new NoteTitle(title), content);
        note.SetPlainText(NoteText.Extract(content, PlainTextMaxLength));
        note.RegisterUpdate();
        _db.NoteRevisions.Add(NoteRevision.Capture(note, RevisionReason.Edited));

        await _db.SaveChangesAsync(ct);
        return note;
    }

    // ---- Revisões (M06) ----

    public async Task<List<RevisionSummaryResponse>?> ListRevisionsAsync(string ownerId, Guid noteId, CancellationToken ct)
    {
        if (!await _db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == ownerId, ct))
        {
            return null;
        }

        return await _db.NoteRevisions.AsNoTracking()
            .Where(r => r.NoteId == noteId && r.OwnerId == ownerId)
            .OrderByDescending(r => r.NoteVersion)
            .Select(r => new RevisionSummaryResponse(r.Id, r.NoteVersion, r.Title, r.Reason.ToString(), r.RestoredFromVersion, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<RevisionResponse?> GetRevisionAsync(string ownerId, Guid noteId, Guid revisionId, CancellationToken ct)
    {
        NoteRevision? r = await _db.NoteRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == revisionId && x.NoteId == noteId && x.OwnerId == ownerId, ct);
        if (r is null)
        {
            return null;
        }

        List<Guid> tagIds = r.GetTagIds().ToList();
        List<TagResponse> tags = await _db.Tags.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && tagIds.Contains(t.Id))
            .Select(t => new TagResponse(t.Id, t.Name))
            .ToListAsync(ct);
        string? category = r.CategoryId is null
            ? null
            : await _db.Categories.AsNoTracking().Where(c => c.Id == r.CategoryId && c.OwnerId == ownerId).Select(c => c.Name).SingleOrDefaultAsync(ct);

        return new RevisionResponse(r.Id, r.NoteVersion, r.Title, r.Content ?? string.Empty, r.CategoryId, category, tags,
            r.Reason.ToString(), r.RestoredFromVersion, r.CreatedAt);
    }

    /// <summary>
    /// Restaura uma revisão criando uma NOVA versão (o histórico nunca é reescrito).
    /// Categoria/tags que não existem mais são ignoradas.
    /// </summary>
    public async Task<Note?> RestoreRevisionAsync(string ownerId, Guid noteId, Guid revisionId, long expectedVersion, CancellationToken ct)
    {
        Note? note = await _db.Notes.Include(n => n.Tags).SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == ownerId, ct);
        NoteRevision? revision = note is null
            ? null
            : await _db.NoteRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == revisionId && r.NoteId == noteId && r.OwnerId == ownerId, ct);
        if (note is null || revision is null)
        {
            return null;
        }

        if (note.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("Incompatibilidade de versão");
        }

        if (!note.IsEditable)
        {
            throw new DomainException("Restaure a nota da lixeira antes de restaurar uma revisão.");
        }

        if (!await _db.NoteRevisions.AnyAsync(r => r.NoteId == note.Id && r.NoteVersion == note.Version, ct))
        {
            _db.NoteRevisions.Add(NoteRevision.CaptureBaseline(note));
        }

        note.Update(new NoteTitle(revision.Title), revision.Content);
        note.SetPlainText(NoteText.Extract(revision.Content, PlainTextMaxLength));

        Category? category = revision.CategoryId is null
            ? null
            : await _db.Categories.SingleOrDefaultAsync(c => c.Id == revision.CategoryId && c.OwnerId == ownerId, ct);
        if (category is null)
        {
            note.ClearCategory();
        }
        else
        {
            note.SetCategory(category);
        }

        List<Guid> tagIds = revision.GetTagIds().ToList();
        List<Tag> tags = await _db.Tags.Where(t => t.OwnerId == ownerId && tagIds.Contains(t.Id)).ToListAsync(ct);
        if (tags.Count == 0)
        {
            note.ClearTags();
        }
        else
        {
            note.UpdateTags(tags);
        }

        note.RegisterUpdate();
        _db.NoteRevisions.Add(NoteRevision.Capture(note, RevisionReason.Restored, revision.NoteVersion));
        await _db.SaveChangesAsync(ct);
        return note;
    }

    public async Task<Note?> ChangeLifecycleAsync(string ownerId, Guid id, Action<Note> transition, long? expectedVersion, CancellationToken ct)
    {
        Note? note = await _db.Notes.Include(n => n.Tags).SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
        if (note == null)
        {
            return null;
        }

        if (expectedVersion.HasValue && note.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("Incompatibilidade de versão");
        }

        transition(note);
        await _db.SaveChangesAsync(ct);
        return note;
    }

    public async Task<DeleteResult> DeleteNoteAsync(string ownerId, Guid id, CancellationToken ct)
    {
        Note? note = await _db.Notes.SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
        if (note == null)
        {
            return DeleteResult.NotFound;
        }

        if (note.Status != NoteStatus.Trashed)
        {
            return DeleteResult.NotInTrash;
        }

        // Dúvidas pertencem à nota: removidas junto. Posições, arestas e referências caem por cascade.
        await _db.Doubts.Where(d => d.NoteId == id && d.OwnerId == ownerId).ExecuteDeleteAsync(ct);
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync(ct);
        return DeleteResult.Deleted;
    }

    public async Task<NoteResponse> ToResponseAsync(string ownerId, Note note, CancellationToken ct) =>
        ToResponse(note, await CategoryNamesAsync(ownerId, [note], ct));

    private async Task<Dictionary<Guid, string>> CategoryNamesAsync(string ownerId, IEnumerable<Note> notes, CancellationToken ct)
    {
        List<Guid> ids = notes.Where(n => n.CategoryId.HasValue).Select(n => n.CategoryId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.Categories.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    public static NoteResponse ToResponse(Note n, IReadOnlyDictionary<Guid, string> categories) =>
        new(n.Id, n.Title.Value, n.Content ?? string.Empty, n.Version, n.Status.ToString(),
            n.Tags.Select(t => new TagResponse(t.Id, t.Name)).ToList())
        {
            CategoryId = n.CategoryId,
            CategoryName = n.CategoryId.HasValue && categories.TryGetValue(n.CategoryId.Value, out string? name) ? name : null,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
            TrashedAt = n.TrashedAt,
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
