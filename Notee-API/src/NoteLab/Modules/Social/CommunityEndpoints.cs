using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
namespace NoteLab.Application;

public sealed class Community
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
public sealed class CommunityMember
{
    public Guid CommunityId { get; set; }
    public string UserId { get; set; } = "";
}
public sealed class CommunityPublication
{
    public Guid CommunityId { get; set; }
    public Guid PublicationId { get; set; }
}
public sealed class PublicationComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PublicationId { get; set; }
    public Guid RevisionId { get; set; }
    public string UserId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public record CommunityInput(string Name, string Description);
public record CommentInput(string Text);

public static class CommunityEndpoints
{
    private static string User(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IQueryable<Publication> Public(NoteDbContext db) => db.Publications.Where(p => p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public && p.CurrentRevisionId != null);
    public static void MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/communities").RequireAuthorization().WithTags("Communities");
        api.MapGet("", async (NoteDbContext db, CancellationToken ct) => Results.Ok(await db.Communities.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Description, members = db.CommunityMembers.Count(m => m.CommunityId == c.Id) }).Take(100).ToListAsync(ct)));
        api.MapPost("", async (CommunityInput input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string name = input.Name?.Trim() ?? "", description = input.Description?.Trim() ?? "";
            if (name.Length is < 2 or > 80 || description.Length > 500) return Results.BadRequest(new { detail = "Nome: 2 a 80 caracteres. Descricao: ate 500." });
            var community = new Community { OwnerId = User(user), Name = name, Description = description };
            db.Communities.Add(community);
            db.CommunityMembers.Add(new() { CommunityId = community.Id, UserId = User(user) });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/communities/{community.Id}", new { community.Id });
        });
        api.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            var c = await db.Communities.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.Id, c.Name, c.Description, isOwner = c.OwnerId == me, joined = db.CommunityMembers.Any(m => m.CommunityId == id && m.UserId == me) }).SingleOrDefaultAsync(ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });
        api.MapPut("/{id:guid}/membership", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (!await db.Communities.AnyAsync(c => c.Id == id, ct)) return Results.NotFound();
            string me = User(user);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityMembers\" (\"CommunityId\", \"UserId\") VALUES ({id}, {me}) ON CONFLICT DO NOTHING", ct);
            return Results.NoContent();
        });
        api.MapDelete("/{id:guid}/membership", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            if (await db.Communities.AnyAsync(c => c.Id == id && c.OwnerId == me, ct)) return Results.BadRequest(new { detail = "O criador permanece na comunidade." });
            await db.CommunityMembers.Where(m => m.CommunityId == id && m.UserId == me).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        api.MapPut("/{id:guid}/publications/{publicationId:guid}", async (Guid id, Guid publicationId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            if (!await db.CommunityMembers.AnyAsync(m => m.CommunityId == id && m.UserId == me, ct)) return Results.Forbid();
            if (!await Public(db).AnyAsync(p => p.Id == publicationId && p.OwnerId == me, ct)) return Results.NotFound();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityPublications\" (\"CommunityId\", \"PublicationId\") VALUES ({id}, {publicationId}) ON CONFLICT DO NOTHING", ct);
            return Results.NoContent();
        });
        api.MapDelete("/{id:guid}/publications/{publicationId:guid}", async (Guid id, Guid publicationId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            if (!await db.Communities.AnyAsync(c => c.Id == id && c.OwnerId == me, ct) && !await db.Publications.AnyAsync(p => p.Id == publicationId && p.OwnerId == me, ct)) return Results.Forbid();
            await db.CommunityPublications.Where(x => x.CommunityId == id && x.PublicationId == publicationId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        api.MapGet("/{id:guid}/notes", async (Guid id, NoteDbContext db, CancellationToken ct, string sort = "recent", int page = 1) =>
        {
            if (sort is not ("recent" or "likes" or "reposts" or "combined")) return Results.BadRequest();
            var query = from p in Public(db)
                join link in db.CommunityPublications on p.Id equals link.PublicationId
                join r in db.PublicationRevisions on p.CurrentRevisionId equals r.Id
                join profile in db.PublicProfiles on p.OwnerId equals profile.UserId
                where link.CommunityId == id
                select new { p.Id, r.Title, p.Slug, profile.Handle, profile.DisplayName, p.CreatedAt,
                    likes = db.SocialReactions.Count(x => x.PublicationId == p.Id && x.Kind == "like"),
                    reposts = db.SocialReactions.Count(x => x.PublicationId == p.Id && x.Kind == "repost") };
            var ordered = sort == "likes" ? query.OrderByDescending(x => x.likes).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                : sort == "reposts" ? query.OrderByDescending(x => x.reposts).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                : sort == "combined" ? query.OrderByDescending(x => x.likes + x.reposts).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                : query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);
            return Results.Ok(await ordered.Skip((Math.Clamp(page, 1, 10000) - 1) * 20).Take(20).ToListAsync(ct));
        });
        var comments = app.MapGroup("/discussion").RequireAuthorization();
        comments.MapGet("/{id:guid}", async (Guid id, NoteDbContext db, CancellationToken ct) =>
        {
            if (!await Public(db).AnyAsync(p => p.Id == id, ct)) return Results.NotFound();
            return Results.Ok(await (from c in db.PublicationComments
                join u in db.PublicProfiles on c.UserId equals u.UserId
                where c.PublicationId == id orderby c.CreatedAt, c.Id
                select new { c.Id, c.Text, c.RevisionId, c.CreatedAt, u.Handle, u.DisplayName }).Take(100).ToListAsync(ct));
        });
        comments.MapPost("/{id:guid}", async (Guid id, CommentInput input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string text = input.Text?.Trim() ?? "", me = User(user);
            if (text.Length is < 1 or > 2000) return Results.BadRequest(new { detail = "Comentario: 1 a 2000 caracteres." });
            var p = await Public(db).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (p is null) return Results.NotFound();
            if (!await db.PublicProfiles.AnyAsync(x => x.UserId == me, ct)) return Results.BadRequest(new { detail = "Configure seu perfil publico para comentar." });
            var comment = new PublicationComment { PublicationId = id, RevisionId = p.CurrentRevisionId!.Value, UserId = me, Text = text };
            db.PublicationComments.Add(comment); await db.SaveChangesAsync(ct);
            return Results.Created($"/discussion/{id}", new { comment.Id });
        });
        comments.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            var c = await db.PublicationComments.SingleOrDefaultAsync(c => c.Id == id && c.UserId == me, ct);
            if (c is null) return Results.NotFound();
            db.PublicationComments.Remove(c); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
    }
}
