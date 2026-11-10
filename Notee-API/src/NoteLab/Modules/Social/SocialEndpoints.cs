using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using Npgsql;

namespace NoteLab.Application;

public sealed class SocialFollow
{
    public string UserId { get; set; } = "";
    public string TargetId { get; set; } = "";
}
public sealed class SocialReaction
{
    public string UserId { get; set; } = "";
    public Guid PublicationId { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SocialEndpoints
{
    private static string User(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IQueryable<Publication> Public(NoteDbContext db) => db.Publications.Where(p => p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public && p.CurrentRevisionId != null);

    public static void MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/social").RequireAuthorization().WithTags("Social");
        api.MapPost("/reading/{handle}/{slug}", async (string handle, string slug, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            var profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            if (profile is null) return Results.NotFound();
            var publication = await Public(db).AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == profile.UserId && p.Slug == slug.ToLower(), ct);
            if (publication is null) return Results.NotFound();
            DateOnly day = DateOnly.FromDateTime(DateTime.UtcNow);
            if (publication.OwnerId != me)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PublicationViews\" (\"PublicationId\", \"UserId\", \"Day\") VALUES ({publication.Id}, {me}, {day}) ON CONFLICT DO NOTHING", ct);
            return Results.Ok(new { publication.Id, isOwner = publication.OwnerId == me,
                views = await db.PublicationViews.CountAsync(x => x.PublicationId == publication.Id, ct),
                likes = await db.SocialReactions.CountAsync(x => x.PublicationId == publication.Id && x.Kind == "like", ct),
                reposts = await db.SocialReactions.CountAsync(x => x.PublicationId == publication.Id && x.Kind == "repost", ct),
                liked = await db.SocialReactions.AnyAsync(x => x.PublicationId == publication.Id && x.Kind == "like" && x.UserId == me, ct),
                reposted = await db.SocialReactions.AnyAsync(x => x.PublicationId == publication.Id && x.Kind == "repost" && x.UserId == me, ct) });
        });
        api.MapGet("/profiles/{handle}", async (string handle, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            if (profile is null) return Results.NotFound();
            string me = User(user);
            return Results.Ok(new { profile.Handle, profile.DisplayName, isMe = profile.UserId == me,
                following = await db.SocialFollows.AnyAsync(f => f.UserId == me && f.TargetId == profile.UserId, ct),
                followers = await db.SocialFollows.CountAsync(f => f.TargetId == profile.UserId, ct),
                follows = await db.SocialFollows.CountAsync(f => f.UserId == profile.UserId, ct) });
        });
        api.MapPut("/following/{handle}", async (string handle, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var target = await db.PublicProfiles.SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            if (target is null) return Results.NotFound();
            string me = User(user);
            if (target.UserId == me) return Results.BadRequest(new { detail = "Você não pode seguir a si mesmo." });
            if (!await db.SocialFollows.AnyAsync(f => f.UserId == me && f.TargetId == target.UserId, ct))
            {
                db.SocialFollows.Add(new() { UserId = me, TargetId = target.UserId });
                await SaveIdempotent(db, ct);
            }
            return Results.NoContent();
        });
        api.MapDelete("/following/{handle}", async (string handle, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var target = await db.PublicProfiles.SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            if (target is null) return Results.NotFound();
            string me = User(user);
            await db.SocialFollows.Where(f => f.UserId == me && f.TargetId == target.UserId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        api.MapGet("/feed", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct, int page = 1) =>
        {
            string me = User(user);
            var followed = db.SocialFollows.Where(f => f.UserId == me).Select(f => f.TargetId);
            var rows = await (from p in Public(db)
                join r in db.PublicationRevisions on p.CurrentRevisionId equals r.Id
                join author in db.PublicProfiles on p.OwnerId equals author.UserId
                where followed.Contains(p.OwnerId) || db.SocialReactions.Any(x => x.PublicationId == p.Id && x.Kind == "repost" && followed.Contains(x.UserId))
                orderby p.CreatedAt descending, p.Id
                select new { p.Id, r.Title, p.Slug, author.Handle, author.DisplayName, p.CreatedAt,
                    likes = db.SocialReactions.Count(x => x.PublicationId == p.Id && x.Kind == "like"),
                    reposts = db.SocialReactions.Count(x => x.PublicationId == p.Id && x.Kind == "repost"),
                    liked = db.SocialReactions.Any(x => x.PublicationId == p.Id && x.UserId == me && x.Kind == "like"),
                    reposted = db.SocialReactions.Any(x => x.PublicationId == p.Id && x.UserId == me && x.Kind == "repost") })
                .Skip((Math.Clamp(page, 1, 10000) - 1) * 20).Take(20).ToListAsync(ct);
            return Results.Ok(rows);
        });
        api.MapPut("/publications/{id:guid}/{kind}", async (Guid id, string kind, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (kind != "like" && kind != "repost") return Results.BadRequest();
            var publication = await Public(db).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (publication is null) return Results.NotFound();
            string me = User(user);
            if (kind == "like" && publication.OwnerId == me) return Results.BadRequest(new { detail = "Você não pode avaliar sua própria publicação." });
            if (!await db.SocialReactions.AnyAsync(x => x.PublicationId == id && x.UserId == me && x.Kind == kind, ct))
            {
                db.SocialReactions.Add(new() { PublicationId = id, UserId = me, Kind = kind });
                await SaveIdempotent(db, ct);
            }
            return Results.NoContent();
        });
        api.MapDelete("/publications/{id:guid}/{kind}", async (Guid id, string kind, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (kind != "like" && kind != "repost") return Results.BadRequest();
            string me = User(user);
            await db.SocialReactions.Where(x => x.PublicationId == id && x.UserId == me && x.Kind == kind).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }
    private static async Task SaveIdempotent(NoteDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { db.ChangeTracker.Clear(); }
    }
}
