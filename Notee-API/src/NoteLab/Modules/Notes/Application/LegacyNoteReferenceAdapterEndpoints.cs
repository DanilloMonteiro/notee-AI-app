using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

/// <summary>Compatibilidade das rotas antigas dos mapas, agora apoiadas pela biblioteca e pelas citações.</summary>
public static class LegacyNoteReferenceAdapterEndpoints
{
    public static void MapLegacyNoteReferenceAdapterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes/{noteId:guid}/references").RequireAuthorization().WithTags("NoteReferences");
        group.MapGet("/", async (Guid noteId, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct)) return Results.NotFound();
            var rows = await (from citation in db.SourceCitations.AsNoTracking()
                              join source in db.Sources.AsNoTracking() on citation.SourceId equals source.Id
                              where citation.NoteId == noteId && citation.OwnerId == owner && source.OwnerId == owner
                              orderby citation.CreatedAt
                              select new { Citation = citation, Source = source }).ToListAsync(ct);
            return Results.Ok(rows.Select(x => ToResponse(x.Citation.Id, noteId, x.Source, x.Citation.CreatedAt)).ToList());
        });

        group.MapPost("/", async (Guid noteId, CreateNoteReferenceRequest request, NoteDbContext db,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Note? note = await db.Notes.SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == owner, ct);
            if (note is null) return Results.NotFound();
            if (!note.IsEditable) return Results.Conflict(new { detail = "Restaure a nota da lixeira para adicionar uma fonte." });
            if (await db.SourceCitations.CountAsync(c => c.NoteId == noteId && c.OwnerId == owner, ct) >= 100)
                return Results.Problem(statusCode: 400, title: "Erro de validação", detail: "Limite de 100 referências por nota atingido.");
            if (!Enum.TryParse(request.Kind, true, out NoteReferenceKind oldKind) || !Enum.IsDefined(oldKind) || int.TryParse(request.Kind, out _))
                return Results.Problem(statusCode: 400, title: "Erro de validação", detail: "Tipo deve ser 'book' ou 'source'.");
            SourceKind kind = oldKind == NoteReferenceKind.Book ? SourceKind.Book :
                string.IsNullOrWhiteSpace(request.Url) ? SourceKind.Document : SourceKind.Website;
            int? year = int.TryParse(request.Year, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedYear) ? parsedYear : null;
            try
            {
                var source = new Source(owner, kind, request.Title, oldKind == NoteReferenceKind.Book && !string.IsNullOrWhiteSpace(request.Author) ? [request.Author] : [],
                    year, oldKind == NoteReferenceKind.Source ? request.Publisher : null, null, null, null, request.Url, "unknown", null);
                var citation = new SourceCitation(owner, noteId, source.Id, null, null, null, null,
                    SourceLocatorKind.None, null, null, null, null, null, null);
                db.Sources.Add(source);
                db.SourceCitations.Add(citation);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/notes/{noteId}/references/{citation.Id}", ToResponse(citation.Id, noteId, source, citation.CreatedAt));
            }
            catch (DomainException ex)
            {
                return Results.Problem(statusCode: 400, title: "Erro de validação", detail: ex.Message);
            }
        });

        group.MapDelete("/{referenceId:guid}", async (Guid noteId, Guid referenceId, NoteDbContext db,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            string owner = Owner(user);
            SourceCitation? citation = await db.SourceCitations.SingleOrDefaultAsync(c => c.Id == referenceId && c.NoteId == noteId && c.OwnerId == owner, ct);
            if (citation is null) return Results.NotFound();
            db.SourceCitations.Remove(citation);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static NoteReferenceResponse ToResponse(Guid id, Guid noteId, Source source, DateTimeOffset createdAt) =>
        new(id, noteId, source.Kind == SourceKind.Book ? "book" : "source", source.Title,
            source.Kind == SourceKind.Book ? source.Authors.FirstOrDefault() : null,
            source.Kind == SourceKind.Book ? null : source.Venue,
            source.Year?.ToString(CultureInfo.InvariantCulture), source.Url, createdAt);

    private static string Owner(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
