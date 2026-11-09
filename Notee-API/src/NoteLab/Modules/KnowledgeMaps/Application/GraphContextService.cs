using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

// ---- Contratos ----------------------------------------------------------------

/// <summary>scope: "all" (mapa inteiro) ou "selected" (subgrafo com noteIds).</summary>
public record GraphScopeRequest(string? Scope, List<Guid>? NoteIds);

public record ContextReference(string Kind, string Title, string? Author, string? Publisher, string? Year, string? Url);
public record ContextDoubt(Guid Id, string Question, string Status);
public record ContextNode(Guid Id, long Version, string Title, string? Category, List<string> Tags, string Content, bool ContentTruncated,
    List<ContextReference> References, List<ContextDoubt> Doubts);
public record ContextEdge(Guid Id, Guid From, Guid To, string Type, string? Label);
public record ContextLimits(int MaxNotes, int MaxCharsPerNote);
public record GraphContextResponse(string Schema, Guid BoardId, string BoardTitle, string Scope, DateTimeOffset GeneratedAt,
    int NoteCount, int EdgeCount, bool Truncated, ContextLimits Limits, string ContextHash, List<ContextNode> Nodes, List<ContextEdge> Edges);

public record GraphSuggestionResponse(Guid Id, Guid FromNoteId, string FromTitle, Guid ToNoteId, string ToTitle, string Type,
    string Reason, string Status, string Provider, DateTimeOffset CreatedAt, Guid? CreatedEdgeId);

/// <summary>
/// MAP-04/MAP-05: monta o contexto autorizado do mapa (somente notas elegíveis do dono e arestas internas
/// à seleção) e gera sugestões de conexão que exigem aceite humano. Nada é enviado a serviço externo:
/// o gerador atual é determinístico e local (ADR-024).
/// </summary>
public sealed class GraphContextService
{
    public const string Schema = "notee-graph-context/1";
    public const int MaxNotes = 50;
    public const int MaxCharsPerNote = 2000;
    private const int MaxSuggestionsPerRun = 10;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "sobre", "entre", "quando", "porque", "também", "assim", "ainda", "outro", "outra", "outros", "outras", "sendo",
        "pelos", "pelas", "desta", "deste", "nesta", "neste", "dessa", "desse", "essas", "esses", "estas", "estes",
        "mesmo", "mesma", "todas", "todos", "muito", "muita", "maior", "menor", "forma", "parte", "tempo", "coisa",
    };

    private readonly NoteDbContext _db;
    private readonly BoardService _boards;

    public GraphContextService(NoteDbContext db, BoardService boards)
    {
        _db = db;
        _boards = boards;
    }

    public async Task<MapResult<GraphContextResponse>> BuildAsync(string ownerId, Guid boardId, GraphScopeRequest request, CancellationToken ct)
    {
        Board? board = await _db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == boardId && b.OwnerId == ownerId, ct);
        if (board is null)
        {
            return MapResult<GraphContextResponse>.NotFound("Mapa não encontrado.");
        }

        string scope = (request.Scope ?? "all").ToLowerInvariant();
        if (scope is not ("all" or "selected"))
        {
            return MapResult<GraphContextResponse>.Invalid("Escopo deve ser 'all' ou 'selected'.");
        }

        IQueryable<Note> eligible = _db.Notes.AsNoTracking()
            .Where(n => n.OwnerId == ownerId && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Active));

        if (scope == "selected")
        {
            List<Guid> ids = (request.NoteIds ?? []).Distinct().ToList();
            if (ids.Count == 0)
            {
                return MapResult<GraphContextResponse>.Invalid("Selecione ao menos uma nota.");
            }

            // Somente IDs autorizados: notas de outra conta ou arquivadas são simplesmente ignoradas.
            eligible = eligible.Where(n => ids.Contains(n.Id));
        }

        int total = await eligible.CountAsync(ct);
        List<Note> notes = await eligible.Include(n => n.Tags)
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .Take(MaxNotes)
            .ToListAsync(ct);
        List<Guid> noteIds = notes.Select(n => n.Id).ToList();
        HashSet<Guid> selected = noteIds.ToHashSet();

        List<Guid> categoryIds = notes.Where(n => n.CategoryId.HasValue).Select(n => n.CategoryId!.Value).ToList();
        Dictionary<Guid, string> categories = await _db.Categories.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        ILookup<Guid, NoteReference> refs = (await _db.NoteReferences.AsNoTracking()
            .Where(r => r.OwnerId == ownerId && noteIds.Contains(r.NoteId)).OrderBy(r => r.CreatedAt).ToListAsync(ct))
            .ToLookup(r => r.NoteId);
        ILookup<Guid, Doubt> doubts = (await _db.Doubts.AsNoTracking()
            .Where(d => d.OwnerId == ownerId && noteIds.Contains(d.NoteId)).OrderBy(d => d.CreatedAt).ToListAsync(ct))
            .ToLookup(d => d.NoteId);

        // Só arestas cujas DUAS pontas estão na seleção.
        List<BoardEdge> edges = (await _db.BoardEdges.AsNoTracking().Where(e => e.BoardId == boardId).OrderBy(e => e.CreatedAt).ToListAsync(ct))
            .Where(e => selected.Contains(e.FromNoteId) && selected.Contains(e.ToNoteId))
            .ToList();

        List<ContextNode> nodes = notes.Select(n =>
        {
            string full = NoteText.Extract(n.Content, int.MaxValue);
            bool cut = full.Length > MaxCharsPerNote;
            return new ContextNode(
                n.Id, n.Version, n.Title.Value,
                n.CategoryId.HasValue && categories.TryGetValue(n.CategoryId.Value, out string? cat) ? cat : null,
                n.Tags.Select(t => t.Name).OrderBy(t => t).ToList(),
                cut ? full[..MaxCharsPerNote] : full,
                cut,
                refs[n.Id].Select(r => new ContextReference(r.Kind.ToString().ToLowerInvariant(), r.Title, r.Author, r.Publisher, r.PublicationYear, r.Url)).ToList(),
                // Respostas de IA não revisadas não entram no contexto; só pergunta e estado.
                doubts[n.Id].Select(d => new ContextDoubt(d.Id, d.Question, d.Status.ToString())).ToList());
        }).ToList();

        List<ContextEdge> contextEdges = edges.Select(e => new ContextEdge(e.Id, e.FromNoteId, e.ToNoteId, e.RelationType.ToCode(), e.Label)).ToList();

        string hash = Hash(string.Join("|", notes.Select(n => $"{n.Id}:{n.Version}"))
                           + "#" + string.Join("|", edges.Select(e => $"{e.Id}:{e.Version}")));

        return MapResult<GraphContextResponse>.Ok(new GraphContextResponse(
            Schema, board.Id, board.Title, scope, DateTimeOffset.UtcNow, nodes.Count, contextEdges.Count,
            total > nodes.Count || nodes.Any(n => n.ContentTruncated),
            new ContextLimits(MaxNotes, MaxCharsPerNote), hash, nodes, contextEdges));
    }

    // ---- Sugestões (MAP-05) ----

    public async Task<MapResult<List<GraphSuggestionResponse>>> GenerateAsync(string ownerId, Guid boardId, GraphScopeRequest request, CancellationToken ct)
    {
        MapResult<GraphContextResponse> context = await BuildAsync(ownerId, boardId, request, ct);
        if (!context.IsSuccess)
        {
            return new MapResult<List<GraphSuggestionResponse>>(null, context.Error, context.Message);
        }

        GraphContextResponse ctx = context.Value!;
        var connected = ctx.Edges.Select(e => (e.From, e.To)).Concat(ctx.Edges.Select(e => (e.To, e.From))).ToHashSet();
        var pending = (await _db.GraphSuggestions.AsNoTracking()
                .Where(s => s.BoardId == boardId && s.Status == GraphSuggestionStatus.Pending)
                .Select(s => new { s.FromNoteId, s.ToNoteId }).ToListAsync(ct))
            .SelectMany(s => new[] { (s.FromNoteId, s.ToNoteId), (s.ToNoteId, s.FromNoteId) })
            .ToHashSet();

        Dictionary<Guid, HashSet<string>> words = ctx.Nodes.ToDictionary(n => n.Id, n => Keywords(n.Title + " " + n.Content));
        var candidates = new List<(ContextNode A, ContextNode B, int Score, string Reason)>();

        for (int i = 0; i < ctx.Nodes.Count; i++)
        {
            for (int j = i + 1; j < ctx.Nodes.Count; j++)
            {
                ContextNode a = ctx.Nodes[i], b = ctx.Nodes[j];
                if (connected.Contains((a.Id, b.Id)) || pending.Contains((a.Id, b.Id)))
                {
                    continue;
                }

                var reasons = new List<string>();
                int score = 0;
                List<string> sharedTags = a.Tags.Intersect(b.Tags, StringComparer.OrdinalIgnoreCase).ToList();
                if (sharedTags.Count > 0)
                {
                    score += 3 * sharedTags.Count;
                    reasons.Add($"compartilham a(s) tag(s) {string.Join(", ", sharedTags)}");
                }

                if (a.Category is not null && string.Equals(a.Category, b.Category, StringComparison.OrdinalIgnoreCase))
                {
                    score += 2;
                    reasons.Add($"estão na categoria {a.Category}");
                }

                List<string> sharedWords = words[a.Id].Intersect(words[b.Id]).OrderBy(w => w).Take(4).ToList();
                if (sharedWords.Count > 0)
                {
                    score += sharedWords.Count;
                    reasons.Add($"usam os termos {string.Join(", ", sharedWords)}");
                }

                if (score >= 2)
                {
                    candidates.Add((a, b, score, "As notas " + string.Join("; ", reasons) + "."));
                }
            }
        }

        List<GraphSuggestion> created = candidates
            .OrderByDescending(c => c.Score).ThenBy(c => c.A.Title).ThenBy(c => c.B.Title)
            .Take(MaxSuggestionsPerRun)
            .Select(c => new GraphSuggestion(boardId, ownerId, c.A.Id, c.A.Version, c.B.Id, c.B.Version,
                EdgeRelationType.Related, c.Reason, ctx.ContextHash, "fake"))
            .ToList();

        _db.GraphSuggestions.AddRange(created);
        await _db.SaveChangesAsync(ct);

        return MapResult<List<GraphSuggestionResponse>>.Ok(await ListAsync(ownerId, boardId, "pending", ct) ?? []);
    }

    public async Task<List<GraphSuggestionResponse>?> ListAsync(string ownerId, Guid boardId, string? status, CancellationToken ct)
    {
        if (!await _db.Boards.AnyAsync(b => b.Id == boardId && b.OwnerId == ownerId, ct))
        {
            return null;
        }

        IQueryable<GraphSuggestion> query = _db.GraphSuggestions.AsNoTracking().Where(s => s.BoardId == boardId && s.OwnerId == ownerId);
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Status == GraphSuggestionStatus.Pending);
        }

        List<GraphSuggestion> items = await query.OrderByDescending(s => s.CreatedAt).Take(100).ToListAsync(ct);
        List<Guid> ids = items.SelectMany(s => new[] { s.FromNoteId, s.ToNoteId }).Distinct().ToList();
        Dictionary<Guid, string> titles = (await _db.Notes.AsNoTracking().Where(n => n.OwnerId == ownerId && ids.Contains(n.Id))
                .Select(n => new { n.Id, n.Title }).ToListAsync(ct))
            .ToDictionary(n => n.Id, n => n.Title.Value);

        return items.Select(s => new GraphSuggestionResponse(s.Id, s.FromNoteId, titles.GetValueOrDefault(s.FromNoteId, "Nota indisponível"),
            s.ToNoteId, titles.GetValueOrDefault(s.ToNoteId, "Nota indisponível"), s.RelationType.ToCode(), s.Reason,
            s.Status.ToString(), s.Provider, s.CreatedAt, s.CreatedEdgeId)).ToList();
    }

    /// <summary>Aceite individual: revalida dono, notas e versões; cria a aresta e marca a sugestão na mesma transação.</summary>
    public async Task<MapResult<BoardEdgeResponse>> AcceptAsync(string ownerId, Guid boardId, Guid suggestionId, CancellationToken ct)
    {
        GraphSuggestion? suggestion = await _db.GraphSuggestions
            .SingleOrDefaultAsync(s => s.Id == suggestionId && s.BoardId == boardId && s.OwnerId == ownerId, ct);
        if (suggestion is null)
        {
            return MapResult<BoardEdgeResponse>.NotFound("Sugestão não encontrada.");
        }

        if (suggestion.Status != GraphSuggestionStatus.Pending)
        {
            return MapResult<BoardEdgeResponse>.Conflict("A sugestão já foi decidida.");
        }

        var versions = await _db.Notes.AsNoTracking()
            .Where(n => n.OwnerId == ownerId && (n.Id == suggestion.FromNoteId || n.Id == suggestion.ToNoteId)
                        && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Active))
            .ToDictionaryAsync(n => n.Id, n => n.Version, ct);

        bool stale = !versions.TryGetValue(suggestion.FromNoteId, out long fromV) || fromV != suggestion.FromNoteVersion
                     || !versions.TryGetValue(suggestion.ToNoteId, out long toV) || toV != suggestion.ToNoteVersion;
        if (stale)
        {
            suggestion.MarkStale();
            await _db.SaveChangesAsync(ct);
            return MapResult<BoardEdgeResponse>.Conflict("Sugestão obsoleta: uma das notas mudou ou não está mais disponível. Gere novas sugestões.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        MapResult<BoardEdgeResponse> edge = await _boards.CreateEdgeAsync(ownerId, boardId,
            new CreateEdgeRequest(suggestion.FromNoteId, suggestion.ToNoteId, suggestion.RelationType.ToCode(), null), ct);
        if (!edge.IsSuccess)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            GraphSuggestion? again = await _db.GraphSuggestions.SingleAsync(s => s.Id == suggestionId, ct);
            again.MarkStale();
            await _db.SaveChangesAsync(ct);
            return MapResult<BoardEdgeResponse>.Conflict($"Não foi possível aplicar a sugestão: {edge.Message}");
        }

        suggestion.Accept(edge.Value!.Id);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return edge;
    }

    public async Task<MapResult<bool>> DismissAsync(string ownerId, Guid boardId, Guid suggestionId, CancellationToken ct)
    {
        GraphSuggestion? suggestion = await _db.GraphSuggestions
            .SingleOrDefaultAsync(s => s.Id == suggestionId && s.BoardId == boardId && s.OwnerId == ownerId, ct);
        if (suggestion is null)
        {
            return MapResult<bool>.NotFound("Sugestão não encontrada.");
        }

        if (suggestion.Status != GraphSuggestionStatus.Pending)
        {
            return MapResult<bool>.Conflict("A sugestão já foi decidida.");
        }

        suggestion.Dismiss();
        await _db.SaveChangesAsync(ct);
        return MapResult<bool>.Ok(true);
    }

    private static HashSet<string> Keywords(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}]{5,}")
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .ToHashSet();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32].ToLowerInvariant();
}
