using System.Security.Claims;
using NoteLab.Domain;

namespace NoteLab.Application; // Ajuste para o namespace correto se necessário

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        // Cria um grupo base para todas as rotas começarem com /auth
        var group = app.MapGroup("/auth");

        group.MapGet("/notas", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, int page = 1, int pageSize = 10) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            List<NoteResponse> notesResponse = await repo.GetNotesAsync(ownerId, page, pageSize, ct);

            return Results.Ok(notesResponse);
        }).RequireAuthorization();
        
        group.MapGet("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Note note = await repo.GetByIdAsync(ownerId, id, ct);

            if (note == null)
            {
                return Results.NotFound();
            }

            NoteResponse notesResponse =  new NoteResponse(note.Id, note.Title.Value, note.Status.ToString(), note.Tags.Select(t => new TagResponse(t.Id, t.Name)).ToList());

            return Results.Ok(notesResponse);
        }).RequireAuthorization();

        group.MapPut("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id, UpdateNoteRequest data) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Note note = await repo.UpdateNoteAsync(ownerId, data.ExpectedVersion, id, data.Title, data.Content, data.CategoryId, data.TagIds, ct);

            if (note == null)
            {
                return Results.NotFound();
            }

            NoteResponse notesResponse =  new NoteResponse(note.Id, note.Title.Value, note.Status.ToString(), note.Tags.Select(t => new TagResponse(t.Id, t.Name)).ToList());

            return Results.Ok(notesResponse);
        }).RequireAuthorization();

        group.MapPost("/notas", async (CreateNoteRequest data, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Note note = await repo.AddNoteAsync(ownerId, new Note(new NoteTitle(data.Title), ownerId, data.Content), ct);

            NoteResponse noteResponse = new NoteResponse(note.Id, note.Title.Value, note.Status.ToString(), new List<TagResponse>());

            return Results.Created($"/notas/{noteResponse.Id}", noteResponse);
        }).RequireAuthorization();

        group.MapDelete("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
        {

            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isDeleted = await repo.DeleteNoteAsync(ownerId, id, ct);

            if (isDeleted == false)
            {
                return Results.NoContent();
            }

            return Results.Ok(isDeleted);
        }).RequireAuthorization();

        group.MapPost("/categories", async (CreateCategoryRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) => {

            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var category = new Category(ownerId, request.Name);

            db.Categories.Add(category);

            await db.SaveChangesAsync(ct);

            return Results.Ok(category);
        }).RequireAuthorization();

        group.MapPost("/tags", async (CreateTagRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) => {

            string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var tags = new Tag(ownerId, request.Name);

            db.Tags.Add(tags);

            await db.SaveChangesAsync(ct);

            return Results.Ok(tags);
        }).RequireAuthorization();

    }
}