using Microsoft.EntityFrameworkCore;
using Npgsql;
using NoteLab.Domain;

namespace NoteLab.Application;

/// <summary>
/// Casos de uso do módulo KnowledgeMaps. Todas as operações são isoladas por dono:
/// recurso de outra conta responde como inexistente (404), sem vazar dados.
/// </summary>
public sealed class BoardService
{
    /// <summary>Limite de notas exibidas por mapa (ADR-017, revisar com medição).</summary>
    public const int MaxNodesPerBoard = 500;
    public const int MaxEdgesPerBoard = 2000;
    private const int ExcerptLength = 560;

    private readonly NoteDbContext _db;

    public BoardService(NoteDbContext db)
    {
        _db = db;
    }

    // ---- Catálogo -----------------------------------------------------------

    public async Task<List<BoardSummaryResponse>> ListAsync(string ownerId, CancellationToken ct)
    {
        // Regra da demo v1.2 (ADR-012): toda nota não arquivada aparece em todos os mapas.
        int noteCount = await EligibleNotes(ownerId).CountAsync(ct);
        noteCount = Math.Min(noteCount, MaxNodesPerBoard);

        return await _db.Boards.AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.UpdatedAt).ThenBy(b => b.Id)
            .Select(b => new BoardSummaryResponse(
                b.Id,
                b.Title,
                b.Description,
                b.Version,
                b.UpdatedAt,
                noteCount,
                _db.BoardEdges.Count(e => e.BoardId == b.Id)))
            .ToListAsync(ct);
    }

    public async Task<MapResult<BoardSummaryResponse>> CreateAsync(string ownerId, CreateBoardRequest request, CancellationToken ct)
    {
        Board board;
        try
        {
            board = new Board(ownerId, request.Title, request.Description);
        }
        catch (DomainException ex)
        {
            return MapResult<BoardSummaryResponse>.Invalid(ex.Message);
        }

        _db.Boards.Add(board);
        await _db.SaveChangesAsync(ct);

        int noteCount = Math.Min(await EligibleNotes(ownerId).CountAsync(ct), MaxNodesPerBoard);
        return MapResult<BoardSummaryResponse>.Ok(ToSummary(board, noteCount, 0));
    }

    public async Task<MapResult<BoardSummaryResponse>> UpdateAsync(string ownerId, Guid boardId, UpdateBoardRequest request, CancellationToken ct)
    {
        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        if (board is null)
        {
            return MapResult<BoardSummaryResponse>.NotFound("Mapa não encontrado.");
        }

        if (board.Version != request.ExpectedVersion)
        {
            return MapResult<BoardSummaryResponse>.Conflict("O mapa foi alterado em outro lugar. Recarregue antes de salvar.");
        }

        try
        {
            board.UpdateDetails(request.Title, request.Description);
        }
        catch (DomainException ex)
        {
            return MapResult<BoardSummaryResponse>.Invalid(ex.Message);
        }

        MapResult<bool> saved = await SaveAsync(ct);
        if (!saved.IsSuccess)
        {
            return MapResult<BoardSummaryResponse>.Conflict(saved.Message!);
        }

        int noteCount = Math.Min(await EligibleNotes(ownerId).CountAsync(ct), MaxNodesPerBoard);
        int edgeCount = await _db.BoardEdges.CountAsync(e => e.BoardId == board.Id, ct);
        return MapResult<BoardSummaryResponse>.Ok(ToSummary(board, noteCount, edgeCount));
    }

    /// <summary>Remove somente o mapa, suas posições e conexões. Notas não são apagadas.</summary>
    public async Task<bool> DeleteAsync(string ownerId, Guid boardId, CancellationToken ct)
    {
        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        if (board is null)
        {
            return false;
        }

        _db.Boards.Remove(board);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Leitura do canvas -------------------------------------------------

    public async Task<BoardDetailResponse?> GetAsync(string ownerId, Guid boardId, CancellationToken ct)
    {
        Board? board = await _db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == boardId && b.OwnerId == ownerId, ct);
        if (board is null)
        {
            return null;
        }

        List<Note> notes = await EligibleNotes(ownerId)
            .Include(n => n.Tags)
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .Take(MaxNodesPerBoard)
            .ToListAsync(ct);

        List<Guid> noteIds = notes.Select(n => n.Id).ToList();
        HashSet<Guid> visible = noteIds.ToHashSet();

        List<Guid> categoryIds = notes.Where(n => n.CategoryId.HasValue).Select(n => n.CategoryId!.Value).Distinct().ToList();
        Dictionary<Guid, string> categories = await _db.Categories.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        Dictionary<Guid, BoardPlacement> placements = await _db.BoardPlacements.AsNoTracking()
            .Where(p => p.BoardId == boardId)
            .ToDictionaryAsync(p => p.NoteId, ct);

        List<BoardEdge> edges = await _db.BoardEdges.AsNoTracking()
            .Where(e => e.BoardId == boardId)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToListAsync(ct);

        List<BoardDoubtResponse> doubts = await _db.Doubts.AsNoTracking()
            .Where(d => d.OwnerId == ownerId && noteIds.Contains(d.NoteId))
            .OrderBy(d => d.CreatedAt)
            .Select(d => new BoardDoubtResponse(d.Id, d.NoteId, d.Question, d.Status.ToString()))
            .ToListAsync(ct);

        Dictionary<Guid, int> referenceCounts = await _db.NoteReferences.AsNoTracking()
            .Where(r => r.OwnerId == ownerId && noteIds.Contains(r.NoteId))
            .GroupBy(r => r.NoteId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<Guid, int> doubtCounts = doubts.GroupBy(d => d.NoteId).ToDictionary(g => g.Key, g => g.Count());

        // Posição padrão determinística para notas sem posição gravada.
        // O GET não escreve no banco: a posição só é persistida quando a pessoa move o cartão.
        IEnumerable<BoardPlacement> visiblePlacements = placements.Values.Where(p => visible.Contains(p.NoteId));
        double fallbackBaseY = visiblePlacements.Any() ? visiblePlacements.Max(p => p.Y) + 250 : 150;
        int fallbackIndex = 0;

        List<BoardNodeResponse> nodes = new(notes.Count);
        foreach (Note note in notes)
        {
            double x;
            double y;
            long? positionVersion = null;

            if (placements.TryGetValue(note.Id, out BoardPlacement? placement))
            {
                x = placement.X;
                y = placement.Y;
                positionVersion = placement.PositionVersion;
            }
            else
            {
                x = 120 + (fallbackIndex % 4) * 295;
                y = fallbackBaseY + (fallbackIndex / 4) * 250;
                fallbackIndex++;
            }

            nodes.Add(new BoardNodeResponse(
                note.Id,
                note.Title.Value,
                NoteText.Extract(note.Content, ExcerptLength),
                note.Status.ToString(),
                note.CategoryId.HasValue && categories.TryGetValue(note.CategoryId.Value, out string? cat) ? cat : null,
                note.Tags.Select(t => new TagResponse(t.Id, t.Name)).ToList(),
                x,
                y,
                positionVersion,
                referenceCounts.GetValueOrDefault(note.Id),
                doubtCounts.GetValueOrDefault(note.Id)));
        }

        // Conexões com uma ponta arquivada ficam preservadas, mas não são exibidas.
        List<BoardEdgeResponse> edgeResponses = edges
            .Where(e => visible.Contains(e.FromNoteId) && visible.Contains(e.ToNoteId))
            .Select(ToEdgeResponse)
            .ToList();

        return new BoardDetailResponse(
            board.Id,
            board.Title,
            board.Description,
            board.Version,
            board.UpdatedAt,
            nodes,
            edgeResponses,
            doubts);
    }

    // ---- Posições ------------------------------------------------------------

    public async Task<MapResult<PlacementResponse>> MovePlacementAsync(
        string ownerId, Guid boardId, Guid noteId, MovePlacementRequest request, CancellationToken ct)
    {
        MapResult<List<PlacementResponse>> result = await MovePlacementsAsync(
            ownerId,
            boardId,
            new BatchPlacementsRequest([new PlacementItemRequest(noteId, request.X, request.Y, request.ExpectedVersion)]),
            ct);

        return result.IsSuccess
            ? MapResult<PlacementResponse>.Ok(result.Value![0])
            : new MapResult<PlacementResponse>(default, result.Error, result.Message);
    }

    /// <summary>Grava várias posições de forma atômica (usado por "Reorganizar").</summary>
    public async Task<MapResult<List<PlacementResponse>>> MovePlacementsAsync(
        string ownerId, Guid boardId, BatchPlacementsRequest request, CancellationToken ct)
    {
        List<PlacementItemRequest> items = request.Placements ?? [];
        if (items.Count == 0 || items.Count > MaxNodesPerBoard)
        {
            return MapResult<List<PlacementResponse>>.Invalid($"Informe entre 1 e {MaxNodesPerBoard} posições.");
        }

        if (items.Select(i => i.NoteId).Distinct().Count() != items.Count)
        {
            return MapResult<List<PlacementResponse>>.Invalid("Cada nota pode aparecer apenas uma vez.");
        }

        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        if (board is null)
        {
            return MapResult<List<PlacementResponse>>.NotFound("Mapa não encontrado.");
        }

        List<Guid> noteIds = items.Select(i => i.NoteId).ToList();
        int eligibleCount = await EligibleNotes(ownerId).CountAsync(n => noteIds.Contains(n.Id), ct);
        if (eligibleCount != noteIds.Count)
        {
            return MapResult<List<PlacementResponse>>.NotFound("Nota não encontrada ou indisponível no mapa.");
        }

        Dictionary<Guid, BoardPlacement> existing = await _db.BoardPlacements
            .Where(p => p.BoardId == boardId && noteIds.Contains(p.NoteId))
            .ToDictionaryAsync(p => p.NoteId, ct);

        List<BoardPlacement> touched = new(items.Count);
        try
        {
            foreach (PlacementItemRequest item in items)
            {
                if (existing.TryGetValue(item.NoteId, out BoardPlacement? placement))
                {
                    if (item.ExpectedVersion != placement.PositionVersion)
                    {
                        return MapResult<List<PlacementResponse>>.Conflict(
                            "A posição desta nota foi alterada em outra aba. Recarregue o mapa.");
                    }

                    placement.MoveTo(item.X, item.Y);
                }
                else
                {
                    placement = new BoardPlacement(boardId, item.NoteId, item.X, item.Y);
                    _db.BoardPlacements.Add(placement);
                }

                touched.Add(placement);
            }
        }
        catch (DomainException ex)
        {
            return MapResult<List<PlacementResponse>>.Invalid(ex.Message);
        }

        board.Touch();

        MapResult<bool> saved = await SaveAsync(ct);
        if (!saved.IsSuccess)
        {
            return MapResult<List<PlacementResponse>>.Conflict(saved.Message!);
        }

        return MapResult<List<PlacementResponse>>.Ok(
            touched.Select(p => new PlacementResponse(p.NoteId, p.X, p.Y, p.PositionVersion)).ToList());
    }

    // ---- Conexões ------------------------------------------------------------

    public async Task<MapResult<BoardEdgeResponse>> CreateEdgeAsync(
        string ownerId, Guid boardId, CreateEdgeRequest request, CancellationToken ct)
    {
        if (!EdgeRelationTypeCodes.TryParse(request.Type, out EdgeRelationType type))
        {
            return MapResult<BoardEdgeResponse>.Invalid("Tipo de relação inválido.");
        }

        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        if (board is null)
        {
            return MapResult<BoardEdgeResponse>.NotFound("Mapa não encontrado.");
        }

        MapResult<BoardEdgeResponse>? endpointError = await ValidateEndpointsAsync(ownerId, request.FromNoteId, request.ToNoteId, ct);
        if (endpointError is not null)
        {
            return endpointError;
        }

        List<BoardEdge> boardEdges = await _db.BoardEdges.Where(e => e.BoardId == boardId).ToListAsync(ct);
        if (boardEdges.Count >= MaxEdgesPerBoard)
        {
            return MapResult<BoardEdgeResponse>.Invalid($"Limite de {MaxEdgesPerBoard} conexões por mapa atingido.");
        }

        if (boardEdges.Any(e => e.IsEquivalentTo(request.FromNoteId, request.ToNoteId, type, request.Label)))
        {
            return MapResult<BoardEdgeResponse>.Conflict("Já existe uma conexão igual neste mapa.");
        }

        BoardEdge edge;
        try
        {
            edge = new BoardEdge(boardId, request.FromNoteId, request.ToNoteId, type, request.Label);
        }
        catch (DomainException ex)
        {
            return MapResult<BoardEdgeResponse>.Invalid(ex.Message);
        }

        _db.BoardEdges.Add(edge);
        board.Touch();
        await _db.SaveChangesAsync(ct);

        return MapResult<BoardEdgeResponse>.Ok(ToEdgeResponse(edge));
    }

    public async Task<MapResult<BoardEdgeResponse>> UpdateEdgeAsync(
        string ownerId, Guid boardId, Guid edgeId, UpdateEdgeRequest request, CancellationToken ct)
    {
        if (!EdgeRelationTypeCodes.TryParse(request.Type, out EdgeRelationType type))
        {
            return MapResult<BoardEdgeResponse>.Invalid("Tipo de relação inválido.");
        }

        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        BoardEdge? edge = board is null
            ? null
            : await _db.BoardEdges.SingleOrDefaultAsync(e => e.Id == edgeId && e.BoardId == boardId, ct);
        if (board is null || edge is null)
        {
            return MapResult<BoardEdgeResponse>.NotFound("Conexão não encontrada.");
        }

        if (edge.Version != request.ExpectedVersion)
        {
            return MapResult<BoardEdgeResponse>.Conflict("A conexão foi alterada em outra aba. Recarregue o mapa.");
        }

        MapResult<BoardEdgeResponse>? endpointError = await ValidateEndpointsAsync(ownerId, request.FromNoteId, request.ToNoteId, ct);
        if (endpointError is not null)
        {
            return endpointError;
        }

        bool duplicate = await _db.BoardEdges
            .Where(e => e.BoardId == boardId && e.Id != edgeId && e.FromNoteId == request.FromNoteId && e.ToNoteId == request.ToNoteId)
            .AnyAsync(e => e.RelationType == type
                           && (e.Label ?? "").ToLower() == (BoardEdge.NormalizeLabel(request.Label) ?? "").ToLower(), ct);
        if (duplicate)
        {
            return MapResult<BoardEdgeResponse>.Conflict("Já existe uma conexão igual neste mapa.");
        }

        try
        {
            edge.Update(request.FromNoteId, request.ToNoteId, type, request.Label);
        }
        catch (DomainException ex)
        {
            return MapResult<BoardEdgeResponse>.Invalid(ex.Message);
        }

        board.Touch();

        MapResult<bool> saved = await SaveAsync(ct);
        return saved.IsSuccess
            ? MapResult<BoardEdgeResponse>.Ok(ToEdgeResponse(edge))
            : MapResult<BoardEdgeResponse>.Conflict(saved.Message!);
    }

    public async Task<bool> DeleteEdgeAsync(string ownerId, Guid boardId, Guid edgeId, CancellationToken ct)
    {
        Board? board = await FindBoardAsync(ownerId, boardId, ct);
        if (board is null)
        {
            return false;
        }

        BoardEdge? edge = await _db.BoardEdges.SingleOrDefaultAsync(e => e.Id == edgeId && e.BoardId == boardId, ct);
        if (edge is null)
        {
            return false;
        }

        _db.BoardEdges.Remove(edge);
        board.Touch();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Auxiliares ----------------------------------------------------------

    private IQueryable<Note> EligibleNotes(string ownerId) =>
        _db.Notes.AsNoTracking().Where(n => n.OwnerId == ownerId && n.Status != NoteStatus.Archived);

    private Task<Board?> FindBoardAsync(string ownerId, Guid boardId, CancellationToken ct) =>
        boardId == Guid.Empty
            ? Task.FromResult<Board?>(null)
            : _db.Boards.SingleOrDefaultAsync(b => b.Id == boardId && b.OwnerId == ownerId, ct);

    private async Task<MapResult<BoardEdgeResponse>?> ValidateEndpointsAsync(
        string ownerId, Guid fromNoteId, Guid toNoteId, CancellationToken ct)
    {
        if (fromNoteId == toNoteId)
        {
            return MapResult<BoardEdgeResponse>.Invalid("Uma nota não pode ser conectada a ela mesma.");
        }

        int found = await EligibleNotes(ownerId).CountAsync(n => n.Id == fromNoteId || n.Id == toNoteId, ct);
        return found == 2 ? null : MapResult<BoardEdgeResponse>.NotFound("Nota de origem ou destino não encontrada.");
    }

    private async Task<MapResult<bool>> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return MapResult<bool>.Ok(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return MapResult<bool>.Conflict("Os dados foram alterados por outra requisição. Recarregue o mapa.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return MapResult<bool>.Conflict("Os dados foram alterados por outra requisição. Recarregue o mapa.");
        }
    }

    private static BoardSummaryResponse ToSummary(Board board, int noteCount, int edgeCount) =>
        new(board.Id, board.Title, board.Description, board.Version, board.UpdatedAt, noteCount, edgeCount);

    private static BoardEdgeResponse ToEdgeResponse(BoardEdge edge) =>
        new(edge.Id, edge.FromNoteId, edge.ToNoteId, edge.RelationType.ToCode(), edge.Label, edge.Version);
}
