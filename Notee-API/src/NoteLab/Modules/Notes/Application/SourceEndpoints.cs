using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

public sealed record SourceInput(string Kind, string Title, List<string>? Authors, int? Year, string? Venue,
    string? Language, string? Doi, string? Isbn, string? Url, string? License, string? Notes);
public sealed record SourceNoteUse(Guid NoteId, string Title);
public sealed record LibrarySourceResponse(Guid Id, string Kind, string Title, List<string> Authors, int? Year,
    string? Venue, string? Language, string? Doi, string? Isbn, string? Url, string License, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int CitationCount, List<SourceNoteUse> CitedNotes,
    string Status, DateTimeOffset? LastCheckedAt);
public sealed record SourceCitationInput(Guid SourceId, string? BlockId, string? ExactQuote, int? StartIndex,
    int? EndIndex, string? LocatorKind, string? LocatorText, int? PageStart, int? PageEnd,
    int? TimeStartSec, int? TimeEndSec, string? SourceQuote);
public sealed record SourceCitationResponse(Guid Id, Guid SourceId, Guid NoteId, string? BlockId,
    string? ExactQuote, int? StartIndex, int? EndIndex, string LocatorKind, string? LocatorText,
    int? PageStart, int? PageEnd, int? TimeStartSec, int? TimeEndSec, string? SourceQuote,
    DateTimeOffset CreatedAt, LibrarySourceResponse Source);

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var sources = app.MapGroup("/sources").RequireAuthorization().WithTags("Sources");
        sources.MapGet("/", ListAsync);
        sources.MapPost("/", CreateAsync);
        sources.MapGet("/{id:guid}", GetAsync);
        sources.MapPatch("/{id:guid}", UpdateAsync);
        sources.MapDelete("/{id:guid}", DeleteAsync);
        sources.MapGet("/{id:guid}/checks", ListChecksAsync);
        sources.MapPost("/{id:guid}/checks", RetryCheckAsync);

        var notes = app.MapGroup("/notes").RequireAuthorization();
        notes.MapGet("/{noteId:guid}/citations", ListCitationsAsync);
        notes.MapPost("/{noteId:guid}/citations", CreateCitationAsync);
        var citations = app.MapGroup("/citations").RequireAuthorization().WithTags("Sources");
        citations.MapPatch("/{id:guid}", UpdateCitationAsync);
        citations.MapDelete("/{id:guid}", DeleteCitationAsync);
    }

    private static async Task<IResult> ListAsync(string? query, string? kind, string? status, NoteDbContext db,
        ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        IQueryable<Source> request = db.Sources.AsNoTracking().Where(s => s.OwnerId == owner);
        SourceCheckStatus? requestedStatus = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!ParseKind(kind, out SourceKind parsed)) return BadRequest("Tipo de fonte inválido.");
            request = request.Where(s => s.Kind == parsed);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim()}%";
            request = request.Where(s => EF.Functions.ILike(s.Title, pattern) ||
                (s.Doi != null && EF.Functions.ILike(s.Doi, pattern)) ||
                (s.Isbn != null && EF.Functions.ILike(s.Isbn, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("unverified", StringComparison.OrdinalIgnoreCase))
                request = request.Where(s => !db.SourceChecks.Any(c => c.OwnerId == owner && c.SourceId == s.Id));
            else
            {
                if (!Enum.TryParse(status, true, out SourceCheckStatus parsed) || !Enum.IsDefined(parsed)) return BadRequest("Status de verificação inválido.");
                requestedStatus = parsed;
            }
        }
        List<Source> rows = await request.OrderBy(s => s.Title).ThenBy(s => s.Id).Take(500).ToListAsync(ct);
        if (requestedStatus is { } filter)
        {
            Guid[] ids = rows.Select(s => s.Id).ToArray();
            List<SourceCheck> checks = await db.SourceChecks.AsNoTracking().Where(c => c.OwnerId == owner && ids.Contains(c.SourceId))
                .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
            HashSet<Guid> matching = checks.GroupBy(c => new { c.SourceId, c.Layer }).Select(g => g.First())
                .GroupBy(c => c.SourceId).Where(g => g.OrderByDescending(c => StatusSeverity(c.Status)).First().Status == filter)
                .Select(g => g.Key).ToHashSet();
            rows = rows.Where(s => matching.Contains(s.Id)).ToList();
        }
        return Results.Ok(await WithUsageAsync(db, owner, rows, ct));
    }

    private static async Task<IResult> GetAsync(Guid id, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        Source? source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id && s.OwnerId == owner, ct);
        return source is null ? Results.NotFound() : Results.Ok((await WithUsageAsync(db, owner, [source], ct)).Single());
    }

    private static async Task<IResult> CreateAsync(SourceInput input, NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        if (!ParseKind(input.Kind, out SourceKind kind)) return BadRequest("Tipo de fonte inválido.");
        Source source;
        try { source = Make(Owner(user), kind, input); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        if (await DuplicateAsync(db, source, null, ct)) return Conflict("Já existe uma fonte com este DOI ou ISBN na biblioteca.");
        db.Sources.Add(source);
        int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
        bool withinQuota = await db.SourceChecks.CountAsync(c => c.OwnerId == source.OwnerId && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) < dailyLimit;
        var check = new SourceCheck(source.OwnerId, source.Id, SourceCheckLayer.Existence);
        if (!withinQuota) check.Complete(SourceCheckStatus.Unverifiable, "Cota diária de verificações atingida; solicite uma nova checagem amanhã.", Array.Empty<object>(), "quota");
        db.SourceChecks.Add(check);
        if (withinQuota) db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return Conflict("Já existe uma fonte com este DOI ou ISBN na biblioteca."); }
        return Results.Created($"/sources/{source.Id}", (await WithUsageAsync(db, source.OwnerId, [source], ct)).Single());
    }

    private static async Task<IResult> UpdateAsync(Guid id, SourceInput input, NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == id && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        if (!ParseKind(input.Kind, out SourceKind kind)) return BadRequest("Tipo de fonte inválido.");
        try { source.Update(kind, input.Title, input.Authors, input.Year, input.Venue, input.Language, input.Doi, input.Isbn, input.Url, input.License, input.Notes); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        if (await DuplicateAsync(db, source, id, ct)) return Conflict("Já existe uma fonte com este DOI ou ISBN na biblioteca.");
        int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
        bool withinQuota = await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) < dailyLimit;
        var check = new SourceCheck(owner, source.Id, SourceCheckLayer.Existence);
        if (!withinQuota) check.Complete(SourceCheckStatus.Unverifiable, "Cota diária de verificações atingida; solicite uma nova checagem amanhã.", Array.Empty<object>(), "quota");
        db.SourceChecks.Add(check);
        if (withinQuota) db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return Conflict("Já existe uma fonte com este DOI ou ISBN na biblioteca."); }
        return Results.Ok((await WithUsageAsync(db, owner, [source], ct)).Single());
    }

    private static async Task<IResult> ListChecksAsync(Guid id, string? status, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        if (!await db.Sources.AnyAsync(s => s.Id == id && s.OwnerId == owner, ct)) return Results.NotFound();
        IQueryable<SourceCheck> query = db.SourceChecks.AsNoTracking().Where(c => c.SourceId == id && c.OwnerId == owner);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, true, out SourceCheckStatus parsed) || !Enum.IsDefined(parsed)) return BadRequest("Status de verificação inválido.");
            query = query.Where(c => c.Status == parsed);
        }
        var rows = await query.OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync(ct);
        return Results.Ok(rows.Select(c => new { c.Id, layer = c.Layer.ToString().ToLowerInvariant(), status = c.Status.ToString().ToLowerInvariant(), c.Summary,
            arguments = JsonSerializer.Deserialize<List<SourceCheckArgument>>(c.ArgumentsJson) ?? [], c.Provider, c.Verdict, c.InputTokens, c.OutputTokens, c.CostUsd, c.CreatedAt, c.CheckedAt }));
    }

    private static async Task<IResult> RetryCheckAsync(Guid id, NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == id && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
        if (await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) >= dailyLimit)
            return Results.Problem(statusCode: 429, title: "Cota de verificação atingida", detail: $"Limite de {dailyLimit} verificações por dia atingido.");
        var check = new SourceCheck(owner, id, SourceCheckLayer.Existence);
        db.SourceChecks.Add(check); db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
        await db.SaveChangesAsync(ct);
        return Results.Accepted($"/sources/{id}/checks", new { checkId = check.Id, status = "pending" });
    }

    private static async Task<IResult> DeleteAsync(Guid id, NoteDbContext db, SourceArchiveStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == id && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        if (await db.SourceCitations.AnyAsync(c => c.SourceId == id && c.OwnerId == owner, ct))
            return Conflict("A fonte está citada. Remova primeiro as citações das notas.");
        await storage.DeleteSourceAsync(db, owner, id, ct);
        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListCitationsAsync(Guid noteId, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct)) return Results.NotFound();
        List<SourceCitation> rows = await db.SourceCitations.AsNoTracking().Where(c => c.NoteId == noteId && c.OwnerId == owner)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);
        var sourceIds = rows.Select(c => c.SourceId).Distinct().ToArray();
        List<Source> sources = await db.Sources.AsNoTracking().Where(s => s.OwnerId == owner && sourceIds.Contains(s.Id)).ToListAsync(ct);
        var sourceMap = (await WithUsageAsync(db, owner, sources, ct)).ToDictionary(s => s.Id);
        return Results.Ok(rows.Where(c => sourceMap.ContainsKey(c.SourceId)).Select(c => ToCitation(c, sourceMap[c.SourceId])).ToList());
    }

    private static async Task<IResult> CreateCitationAsync(Guid noteId, SourceCitationInput input,
        NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        Note? note = await db.Notes.SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == owner, ct);
        if (note is null) return Results.NotFound();
        if (!note.IsEditable) return Conflict("Restaure a nota da lixeira para citar uma fonte.");
        if (await db.SourceCitations.CountAsync(c => c.NoteId == noteId && c.OwnerId == owner, ct) >= 100)
            return BadRequest("Limite de 100 citações por nota atingido.");
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == input.SourceId && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        if (!ValidAnchor(note.Content, input)) return BadRequest("O trecho não corresponde à versão salva da nota.");
        try
        {
            var citation = new SourceCitation(owner, noteId, source.Id, input.BlockId, input.ExactQuote,
                input.StartIndex, input.EndIndex, ParseLocator(input.LocatorKind), input.LocatorText,
                input.PageStart, input.PageEnd, input.TimeStartSec, input.TimeEndSec, input.SourceQuote);
            db.SourceCitations.Add(citation);
            int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
            bool withinQuota = await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) < dailyLimit;
            var check = new SourceCheck(owner, source.Id, SourceCheckLayer.Citation, citation.Id);
            if (!withinQuota) check.Complete(SourceCheckStatus.Unverifiable, "Cota diária de verificações atingida; solicite uma nova checagem amanhã.", Array.Empty<object>(), "quota");
            db.SourceChecks.Add(check);
            if (withinQuota)
            {
                db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
            }
            await db.SaveChangesAsync(ct);
            var response = (await WithUsageAsync(db, owner, [source], ct)).Single();
            return Results.Created($"/citations/{citation.Id}", ToCitation(citation, response));
        }
        catch (DomainException ex) { return BadRequest(ex.Message); }
    }

    private static async Task<IResult> UpdateCitationAsync(Guid id, SourceCitationInput input,
        NoteDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        SourceCitation? citation = await db.SourceCitations.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == owner, ct);
        if (citation is null) return Results.NotFound();
        Note? note = await db.Notes.SingleOrDefaultAsync(n => n.Id == citation.NoteId && n.OwnerId == owner, ct);
        if (note is null) return Results.NotFound();
        if (!note.IsEditable) return Conflict("Restaure a nota da lixeira para editar a citação.");
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == input.SourceId && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        if (!ValidAnchor(note.Content, input)) return BadRequest("O trecho não corresponde à versão salva da nota.");
        try
        {
            citation.ReassignSource(source.Id);
            citation.Update(input.BlockId, input.ExactQuote, input.StartIndex, input.EndIndex,
                ParseLocator(input.LocatorKind), input.LocatorText, input.PageStart, input.PageEnd,
                input.TimeStartSec, input.TimeEndSec, input.SourceQuote);
            int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
            bool withinQuota = await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct) < dailyLimit;
            var check = new SourceCheck(owner, source.Id, SourceCheckLayer.Citation, citation.Id);
            if (!withinQuota) check.Complete(SourceCheckStatus.Unverifiable, "Cota diária de verificações atingida; solicite uma nova checagem amanhã.", Array.Empty<object>(), "quota");
            db.SourceChecks.Add(check);
            if (withinQuota)
            {
                db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToCitation(citation, (await WithUsageAsync(db, owner, [source], ct)).Single()));
        }
        catch (DomainException ex) { return BadRequest(ex.Message); }
    }

    private static async Task<IResult> DeleteCitationAsync(Guid id, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        SourceCitation? citation = await db.SourceCitations.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == owner, ct);
        if (citation is null) return Results.NotFound();
        db.SourceCitations.Remove(citation);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public static async Task<List<LibrarySourceResponse>> WithUsageAsync(NoteDbContext db, string owner, List<Source> sources, CancellationToken ct)
    {
        if (sources.Count == 0) return [];
        Guid[] ids = sources.Select(s => s.Id).ToArray();
        var usages = await (from citation in db.SourceCitations.AsNoTracking()
                            join note in db.Notes.AsNoTracking() on citation.NoteId equals note.Id
                            where citation.OwnerId == owner && note.OwnerId == owner && ids.Contains(citation.SourceId)
                            select new { citation.SourceId, NoteId = note.Id }).ToListAsync(ct);
        Guid[] noteIds = usages.Select(u => u.NoteId).Distinct().ToArray();
        Dictionary<Guid, string> noteTitles = (await db.Notes.AsNoTracking()
            .Where(n => n.OwnerId == owner && noteIds.Contains(n.Id)).ToListAsync(ct))
            .ToDictionary(n => n.Id, n => n.Title.Value);
        List<SourceCheck> checks = await db.SourceChecks.AsNoTracking().Where(c => c.OwnerId == owner && ids.Contains(c.SourceId))
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        Dictionary<Guid, SourceCheck> latest = checks.GroupBy(c => new { c.SourceId, c.Layer }).Select(g => g.First())
            .GroupBy(c => c.SourceId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => StatusSeverity(c.Status)).First());
        return sources.Select(s => new LibrarySourceResponse(s.Id, s.Kind.ToString().ToLowerInvariant(), s.Title, s.Authors.ToList(),
            s.Year, s.Venue, s.Language, s.Doi, s.Isbn, s.Url, s.License, s.Notes, s.CreatedAt, s.UpdatedAt,
            usages.Count(u => u.SourceId == s.Id), usages.Where(u => u.SourceId == s.Id && noteTitles.ContainsKey(u.NoteId))
                .Select(u => new SourceNoteUse(u.NoteId, noteTitles[u.NoteId])).DistinctBy(u => u.NoteId)
                .OrderBy(u => u.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            latest.TryGetValue(s.Id, out SourceCheck? check) ? check.Status.ToString().ToLowerInvariant() : "unverified", check?.CheckedAt)).ToList();
    }

    private static int StatusSeverity(SourceCheckStatus status) => status switch
    {
        SourceCheckStatus.NotFound or SourceCheckStatus.Mismatch => 8,
        SourceCheckStatus.QuoteNotFound => 7,
        SourceCheckStatus.FoundWithDifferences => 6,
        SourceCheckStatus.Failed => 5,
        SourceCheckStatus.Running => 4,
        SourceCheckStatus.Pending => 3,
        SourceCheckStatus.Unverifiable => 2,
        _ => 1
    };

    public static SourceCitationResponse ToCitation(SourceCitation c, LibrarySourceResponse source) =>
        new(c.Id, c.SourceId, c.NoteId, c.BlockId, c.ExactQuote, c.StartIndex, c.EndIndex,
            c.LocatorKind.ToString().ToLowerInvariant(), c.LocatorText, c.PageStart, c.PageEnd,
            c.TimeStartSec, c.TimeEndSec, c.SourceQuote, c.CreatedAt, source);

    private static Source Make(string owner, SourceKind kind, SourceInput input) =>
        new(owner, kind, input.Title, input.Authors, input.Year, input.Venue, input.Language,
            input.Doi, input.Isbn, input.Url, input.License, input.Notes);

    private static async Task<bool> DuplicateAsync(NoteDbContext db, Source source, Guid? exceptId, CancellationToken ct) =>
        await db.Sources.AnyAsync(s => s.OwnerId == source.OwnerId && s.Id != exceptId &&
            ((source.Doi != null && s.Doi == source.Doi) || (source.Isbn != null && s.Isbn == source.Isbn)), ct);

    private static bool ParseKind(string? value, out SourceKind kind) =>
        Enum.TryParse(value, true, out kind) && Enum.IsDefined(kind) && !int.TryParse(value, out _);

    private static SourceLocatorKind ParseLocator(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SourceLocatorKind.None;
        if (!Enum.TryParse(value, true, out SourceLocatorKind parsed) || !Enum.IsDefined(parsed) || int.TryParse(value, out _))
            throw new DomainException("Tipo de localização inválido.");
        return parsed;
    }

    private static bool ValidAnchor(string? content, SourceCitationInput input)
    {
        bool hasAnchor = !string.IsNullOrWhiteSpace(input.BlockId) || !string.IsNullOrWhiteSpace(input.ExactQuote) || input.StartIndex.HasValue || input.EndIndex.HasValue;
        if (!hasAnchor) return true;
        if (string.IsNullOrWhiteSpace(input.BlockId) || string.IsNullOrEmpty(input.ExactQuote) || !input.StartIndex.HasValue) return false;
        if (input.EndIndex != input.StartIndex.Value + input.ExactQuote.Length || string.IsNullOrEmpty(content)) return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (JsonElement block in doc.RootElement.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object || !block.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != input.BlockId ||
                    !block.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) continue;
                string value = text.GetString() ?? "";
                int start = input.StartIndex.Value;
                return start >= 0 && start + input.ExactQuote.Length <= value.Length && value.AsSpan(start, input.ExactQuote.Length).SequenceEqual(input.ExactQuote.AsSpan());
            }
        }
        catch (JsonException) { }
        return false;
    }

    private static string Owner(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private static IResult BadRequest(string detail) => Results.Problem(statusCode: 400, title: "Erro de validação", detail: detail);
    private static IResult Conflict(string detail) => Results.Problem(statusCode: 409, title: "Conflito", detail: detail);
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}
