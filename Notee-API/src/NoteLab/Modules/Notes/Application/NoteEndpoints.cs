using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record LifecycleRequest(long? ExpectedVersion);

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").RequireAuthorization();

        // Lista paginada com filtros. Total em X-Total-Count (o corpo continua sendo um array).
        group.MapGet("/", async (INoteRepository repo, ClaimsPrincipal user, HttpResponse response, CancellationToken ct,
            int page = 1, int pageSize = 10, string? status = null, string? query = null, Guid? categoryId = null, Guid? tagId = null) =>
        {
            NotePage result = await repo.GetNotesAsync(Owner(user), new NoteQuery(status, query, categoryId, tagId, page, pageSize), ct);
            response.Headers["X-Total-Count"] = result.Total.ToString();
            response.Headers.AccessControlExposeHeaders = "X-Total-Count";
            return Results.Ok(result.Items);
        });

        group.MapGet("/{id:guid}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
        {
            NoteResponse? note = await repo.GetResponseAsync(Owner(user), id, ct);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        group.MapPut("/{id:guid}", async (NoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id, UpdateNoteRequest data) =>
        {
            string ownerId = Owner(user);
            Note? note = await repo.UpdateNoteAsync(ownerId, data.ExpectedVersion, id, data.Title, data.Content ?? "", data.CategoryId, data.TagIds, ct);
            return note is null ? Results.NotFound() : Results.Ok(await repo.ToResponseAsync(ownerId, note, ct));
        });

        group.MapPost("/", async (CreateNoteRequest data, ClaimsPrincipal user, NoteRepository repo, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Note note = await repo.AddNoteAsync(ownerId, new Note(new NoteTitle(data.Title), ownerId, data.Content), ct);
            NoteResponse response = await repo.ToResponseAsync(ownerId, note, ct);
            return Results.Created($"/notes/{response.Id}", response);
        });

        // ---- Ciclo de vida: arquivar, lixeira, restaurar, excluir definitivamente ----
        MapLifecycle(group, "archive", n => n.Archive());
        MapLifecycle(group, "trash", n => n.MoveToTrash());
        MapLifecycle(group, "restore", n => n.Restore());

        group.MapDelete("/{id:guid}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
            await repo.DeleteNoteAsync(Owner(user), id, ct) switch
            {
                DeleteResult.Deleted => Results.NoContent(),
                DeleteResult.NotInTrash => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito",
                    detail: "Mova a nota para a lixeira antes de excluí-la definitivamente."),
                _ => Results.NotFound(),
            });

        // ---- Revisões (M06) ----
        group.MapGet("/{id:guid}/revisions", async (Guid id, NoteRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            List<RevisionSummaryResponse>? revisions = await repo.ListRevisionsAsync(Owner(user), id, ct);
            return revisions is null ? Results.NotFound() : Results.Ok(revisions);
        });

        group.MapGet("/{id:guid}/revisions/{revisionId:guid}", async (Guid id, Guid revisionId, NoteRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            RevisionResponse? revision = await repo.GetRevisionAsync(Owner(user), id, revisionId, ct);
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });

        group.MapPost("/{id:guid}/restore-revision", async (Guid id, RestoreRevisionRequest request, NoteRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Note? note = await repo.RestoreRevisionAsync(ownerId, id, request.RevisionId, request.ExpectedVersion, ct);
            return note is null ? Results.NotFound() : Results.Ok(await repo.ToResponseAsync(ownerId, note, ct));
        });

        MapTaxonomy(group);
    }

    private static void MapLifecycle(RouteGroupBuilder group, string action, Action<Note> transition)
    {
        group.MapPost($"/{{id:guid}}/{action}", async (Guid id, LifecycleRequest? body, NoteRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Note? note = await repo.ChangeLifecycleAsync(ownerId, id, transition, body?.ExpectedVersion, ct);
            return note is null ? Results.NotFound() : Results.Ok(await repo.ToResponseAsync(ownerId, note, ct));
        });
    }

    // ---- Categorias e tags (isoladas por dono) ----
    private static void MapTaxonomy(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            return Results.Ok(await db.Categories.AsNoTracking()
                .Where(c => c.OwnerId == ownerId)
                .OrderBy(c => c.Name)
                .Select(c => new TaxonomyItemResponse(c.Id, c.Name,
                    db.Notes.Count(n => n.OwnerId == ownerId && n.CategoryId == c.Id && n.Status != NoteStatus.Trashed)))
                .ToListAsync(ct));
        });

        group.MapPost("/categories", async (CreateCategoryRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Category category = Category.Create(ownerId, request.Name);
            if (await db.Categories.AnyAsync(c => c.OwnerId == ownerId && c.Name.ToLower() == category.Name.ToLower(), ct))
            {
                return Duplicate("categoria");
            }

            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/notes/categories/{category.Id}", new TaxonomyItemResponse(category.Id, category.Name, 0));
        });

        group.MapPatch("/categories/{id:guid}", async (Guid id, RenameRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Category? category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, ct);
            if (category is null)
            {
                return Results.NotFound();
            }

            string name = Category.Validate(request.Name);
            if (await db.Categories.AnyAsync(c => c.OwnerId == ownerId && c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            {
                return Duplicate("categoria");
            }

            category.Rename(name);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new TaxonomyItemResponse(category.Id, category.Name,
                await db.Notes.CountAsync(n => n.OwnerId == ownerId && n.CategoryId == id && n.Status != NoteStatus.Trashed, ct)));
        });

        group.MapDelete("/categories/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Category? category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, ct);
            if (category is null)
            {
                return Results.NotFound();
            }

            // As notas ficam sem categoria; nada é apagado nelas além do vínculo.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Notes.Where(n => n.OwnerId == ownerId && n.CategoryId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.CategoryId, (Guid?)null), ct);
            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/tags", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            return Results.Ok(await db.Tags.AsNoTracking()
                .Where(t => t.OwnerId == ownerId)
                .OrderBy(t => t.Name)
                .Select(t => new TaxonomyItemResponse(t.Id, t.Name,
                    db.Notes.Count(n => n.OwnerId == ownerId && n.Status != NoteStatus.Trashed && n.Tags.Any(x => x.Id == t.Id))))
                .ToListAsync(ct));
        });

        group.MapPost("/tags", async (CreateTagRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Tag tag = Tag.Create(ownerId, request.Name);
            if (await db.Tags.AnyAsync(t => t.OwnerId == ownerId && t.Name.ToLower() == tag.Name.ToLower(), ct))
            {
                return Duplicate("tag");
            }

            db.Tags.Add(tag);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/notes/tags/{tag.Id}", new TaxonomyItemResponse(tag.Id, tag.Name, 0));
        });

        group.MapPatch("/tags/{id:guid}", async (Guid id, RenameRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Tag? tag = await db.Tags.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId, ct);
            if (tag is null)
            {
                return Results.NotFound();
            }

            string name = Category.Validate(request.Name);
            if (await db.Tags.AnyAsync(t => t.OwnerId == ownerId && t.Id != id && t.Name.ToLower() == name.ToLower(), ct))
            {
                return Duplicate("tag");
            }

            tag.Rename(name);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new TaxonomyItemResponse(tag.Id, tag.Name, 0));
        });

        group.MapDelete("/tags/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string ownerId = Owner(user);
            Tag? tag = await db.Tags.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId, ct);
            if (tag is null)
            {
                return Results.NotFound();
            }

            db.Tags.Remove(tag); // vínculos em NoteTags caem por cascade
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static IResult Duplicate(string what) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito", detail: $"Já existe uma {what} com este nome.");

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
