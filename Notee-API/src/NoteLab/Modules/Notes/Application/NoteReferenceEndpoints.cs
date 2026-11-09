using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public record CreateNoteReferenceRequest(
    string Kind,
    string Title,
    string? Author,
    string? Publisher,
    string? Year,
    string? Url);

public record NoteReferenceResponse(
    Guid Id,
    Guid NoteId,
    string Kind,
    string Title,
    string? Author,
    string? Publisher,
    string? Year,
    string? Url,
    DateTimeOffset CreatedAt);

/// <summary>
/// Livros e fontes da nota (ADR-016). Pertencem à nota, não ao mapa.
/// Não alteram a versão da nota para não invalidar um editor aberto (ver ADR).
/// </summary>
public static class NoteReferenceEndpoints
{
    private const int MaxReferencesPerNote = 100;

    public static void MapNoteReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes/{noteId:guid}/references").RequireAuthorization().WithTags("NoteReferences");

        group.MapGet("/", async (Guid noteId, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string ownerId = BoardEndpoints.OwnerId(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == ownerId, ct))
            {
                return NotFound();
            }

            List<NoteReferenceResponse> refs = await db.NoteReferences.AsNoTracking()
                .Where(r => r.NoteId == noteId && r.OwnerId == ownerId)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => ToResponse(r))
                .ToListAsync(ct);

            return Results.Ok(refs);
        });

        group.MapPost("/", async (Guid noteId, CreateNoteReferenceRequest request, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string ownerId = BoardEndpoints.OwnerId(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == ownerId, ct))
            {
                return NotFound();
            }

            if (!Enum.TryParse(request.Kind, ignoreCase: true, out NoteReferenceKind kind)
                || !Enum.IsDefined(kind) || int.TryParse(request.Kind, out _))
            {
                return Invalid("Tipo deve ser 'book' ou 'source'.");
            }

            if (await db.NoteReferences.CountAsync(r => r.NoteId == noteId, ct) >= MaxReferencesPerNote)
            {
                return Invalid($"Limite de {MaxReferencesPerNote} referências por nota atingido.");
            }

            NoteReference reference;
            try
            {
                reference = new NoteReference(ownerId, noteId, kind, request.Title, request.Author, request.Publisher, request.Year, request.Url);
            }
            catch (DomainException ex)
            {
                return Invalid(ex.Message);
            }

            db.NoteReferences.Add(reference);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/notes/{noteId}/references/{reference.Id}", ToResponse(reference));
        });

        group.MapDelete("/{referenceId:guid}", async (Guid noteId, Guid referenceId, NoteDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string ownerId = BoardEndpoints.OwnerId(user);
            NoteReference? reference = await db.NoteReferences
                .SingleOrDefaultAsync(r => r.Id == referenceId && r.NoteId == noteId && r.OwnerId == ownerId, ct);
            if (reference is null)
            {
                return NotFound();
            }

            db.NoteReferences.Remove(reference);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static NoteReferenceResponse ToResponse(NoteReference r) =>
        new(r.Id, r.NoteId, r.Kind.ToString().ToLowerInvariant(), r.Title, r.Author, r.Publisher, r.PublicationYear, r.Url, r.CreatedAt);

    private static IResult NotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Não encontrado", detail: "Nota ou referência não encontrada.");

    private static IResult Invalid(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Erro de validação", detail: detail);
}
