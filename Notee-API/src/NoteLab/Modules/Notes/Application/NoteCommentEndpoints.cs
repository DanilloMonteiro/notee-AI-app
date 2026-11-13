using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record NoteCommentInput(string BlockId, string ExactQuote, int StartIndex, int EndIndex, string Text);
public sealed record NoteCommentUpdate(string Text);

public static class NoteCommentEndpoints
{
    public static void MapNoteCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/notes").RequireAuthorization();
        notes.MapGet("/{id:guid}/comments", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (!await Owned(db, user, id).AnyAsync(ct)) return Results.NotFound();
            return Results.Ok(await db.NoteComments.AsNoTracking().Where(c => c.NoteId == id && c.OwnerId == User(user))
                .OrderBy(c => c.CreatedAt).ToListAsync(ct));
        });
        notes.MapPost("/{id:guid}/comments", async (Guid id, NoteCommentInput input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var note = await Owned(db, user, id).SingleOrDefaultAsync(ct);
            if (note is null) return Results.NotFound();
            if (!note.IsEditable) return Results.Conflict(new { detail = "Restaure a nota da lixeira para comentar." });
            string text = input.Text?.Trim() ?? "";
            if (text.Length is < 1 or > 2000 || string.IsNullOrEmpty(input.ExactQuote) || input.ExactQuote.Length > 500 ||
                string.IsNullOrEmpty(input.BlockId) || input.BlockId.Length > 100 || input.StartIndex < 0 || input.EndIndex != input.StartIndex + input.ExactQuote.Length ||
                !AnchorExists(note.Content, input.BlockId, input.ExactQuote, input.StartIndex))
                return Results.BadRequest(new { detail = "Comentário ou trecho inválido na versão salva da nota." });
            var comment = new NoteComment { NoteId = id, OwnerId = User(user), BlockId = input.BlockId,
                ExactQuote = input.ExactQuote, StartIndex = input.StartIndex, EndIndex = input.EndIndex, Text = text };
            db.NoteComments.Add(comment);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/note-comments/{comment.Id}", comment);
        });
        var comments = app.MapGroup("/note-comments").RequireAuthorization();
        comments.MapPatch("/{id:guid}", async (Guid id, NoteCommentUpdate input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var comment = await db.NoteComments.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == User(user), ct);
            if (comment is null) return Results.NotFound();
            string text = input.Text?.Trim() ?? "";
            if (text.Length is < 1 or > 2000) return Results.BadRequest(new { detail = "Comentário: 1 a 2000 caracteres." });
            comment.Text = text; comment.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(comment);
        });
        comments.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var comment = await db.NoteComments.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == User(user), ct);
            if (comment is null) return Results.NotFound();
            db.NoteComments.Remove(comment);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static IQueryable<Note> Owned(NoteDbContext db, ClaimsPrincipal user, Guid id) =>
        db.Notes.Where(n => n.Id == id && n.OwnerId == User(user));
    private static string User(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private static bool AnchorExists(string? content, string blockId, string quote, int start)
    {
        if (string.IsNullOrEmpty(content)) return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (var block in doc.RootElement.EnumerateArray())
            {
                if (!block.TryGetProperty("id", out var id) || id.GetString() != blockId ||
                    !block.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) continue;
                string value = text.GetString() ?? "";
                return start <= value.Length && start + quote.Length <= value.Length && value.Substring(start, quote.Length) == quote;
            }
        }
        catch (JsonException) { }
        return false;
    }
}
