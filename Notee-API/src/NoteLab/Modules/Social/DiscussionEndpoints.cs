using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record DiscussionAnchor(string BlockId, string ExactQuote, int StartIndex);
public sealed record DiscussionCommentInput(string Text, Guid? ParentId = null, DiscussionAnchor? Anchor = null);
public sealed record NotificationsReadInput(List<Guid>? Ids = null);

public static class DiscussionEndpoints
{
    public const string RatePolicy = "social-comments";
    public static void AddCommentRateLimit(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, IConfiguration config)
    {
        int limit = Math.Max(1, config.GetValue<int?>("Social:CommentsPerHour") ?? 60);
        options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    }

    public static void MapDiscussionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discussion").RequireAuthorization();
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, string view = "all", int page = 1, CancellationToken ct = default) =>
        {
            var publication = await Live(db).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (publication is null) return Results.NotFound();
            if (view is not ("all" or "top") || page is < 1 or > 10000) return Results.BadRequest();
            string me = User(user);
            var rows = await (from c in db.PublicationComments.AsNoTracking()
                join u in db.PublicProfiles on c.UserId equals u.UserId
                where c.PublicationId == id && (!c.IsHidden || publication.OwnerId == me || db.PublicationComments.Any(reply => reply.ParentId == c.Id && !reply.IsHidden))
                select new { c.Id, c.Text, c.ParentId, c.IsDeleted, c.RevisionId, c.CreatedAt,
                    c.AnchorBlockId, c.AnchorQuote, c.AnchorStart, c.AnchorEnd, c.IsHidden,
                    u.Handle, u.DisplayName, UserId = c.UserId,
                    likeCount = db.PublicationCommentLikes.Count(l => l.CommentId == c.Id),
                    likedByMe = db.PublicationCommentLikes.Any(l => l.CommentId == c.Id && l.UserId == me) }).ToListAsync(ct);
            var roots = rows.Where(c => c.ParentId is null).ToList();
            if (view == "top")
            {
                roots = roots.GroupBy(c => (c.AnchorBlockId, c.AnchorQuote))
                    .SelectMany(g => g.OrderByDescending(c => c.likeCount).ThenByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
                        .Take(g.Key.AnchorQuote is null && g.Count() <= 10 ? g.Count() : g.Key.AnchorQuote is null ? 5 : 3)).ToList();
            }
            else roots = roots.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * 50).Take(50).ToList();
            var selected = roots.Select(c => c.Id).ToHashSet();
            var display = view == "top"
                ? roots.SelectMany(c => new[] { c }.Concat(rows.Where(reply => reply.ParentId == c.Id).OrderBy(reply => reply.CreatedAt)))
                : rows.Where(c => selected.Contains(c.Id) || c.ParentId is Guid parent && selected.Contains(parent)).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);
            return Results.Ok(display.Select(c => new {
                    c.Id, Text = c.IsHidden && publication.OwnerId != me ? "" : c.Text,
                    c.ParentId, IsDeleted = c.IsDeleted || c.IsHidden && publication.OwnerId != me, c.RevisionId, c.CreatedAt,
                    c.AnchorBlockId, c.AnchorQuote, c.AnchorStart, c.AnchorEnd,
                    hidden = c.IsHidden && publication.OwnerId == me, c.Handle, c.DisplayName, c.likeCount, c.likedByMe,
                    canDelete = c.UserId == me && !c.IsDeleted, canHide = publication.OwnerId == me,
                    moreCount = c.ParentId is null && view == "top" ? rows.Count(x => x.ParentId is null && x.AnchorBlockId == c.AnchorBlockId && x.AnchorQuote == c.AnchorQuote && !selected.Contains(x.Id)) : 0
                }).ToList());
        });

        group.MapPost("/{id:guid}", async (Guid id, DiscussionCommentInput input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user), text = input.Text?.Trim() ?? "";
            if (text.Length is < 1 or > 2000) return Results.BadRequest(new { detail = "Comentário: 1 a 2000 caracteres." });
            var publication = await Live(db).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (publication is null) return Results.NotFound();
            if (!publication.CommentsEnabled) return Results.Conflict(new { detail = "Comentários desligados." });
            if (!await db.PublicProfiles.AnyAsync(p => p.UserId == me, ct)) return Results.BadRequest(new { detail = "Configure seu perfil público para comentar." });
            PublicationComment? parent = null;
            if (input.ParentId is Guid parentId)
            {
                parent = await db.PublicationComments.SingleOrDefaultAsync(c => c.Id == parentId && c.PublicationId == id && c.ParentId == null && !c.IsDeleted && !c.IsHidden, ct);
                if (parent is null) return Results.BadRequest(new { detail = "Resposta inválida." });
            }
            var anchor = parent is null ? input.Anchor : null;
            if (parent is null && anchor is not null)
            {
                var revision = await db.PublicationRevisions.AsNoTracking().SingleAsync(r => r.Id == publication.CurrentRevisionId, ct);
                if (!ValidAnchor(revision.Content, anchor)) return Results.BadRequest(new { detail = "Trecho inválido na revisão publicada." });
            }
            var comment = new PublicationComment { ParentId = parent?.Id, PublicationId = id,
                RevisionId = publication.CurrentRevisionId!.Value, UserId = me, Text = text,
                AnchorBlockId = parent?.AnchorBlockId ?? anchor?.BlockId, AnchorQuote = parent?.AnchorQuote ?? anchor?.ExactQuote,
                AnchorStart = parent?.AnchorStart ?? anchor?.StartIndex,
                AnchorEnd = parent?.AnchorEnd ?? (anchor is null ? null : anchor.StartIndex + anchor.ExactQuote.Length) };
            db.PublicationComments.Add(comment);
            string recipient = parent?.UserId ?? publication.OwnerId;
            if (recipient != me) db.Notifications.Add(new Notification { UserId = recipient, Kind = parent is null ? "comment" : "reply",
                PublicationId = id, CommentId = comment.Id, ActorUserId = me });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/discussion/{id}", new { comment.Id });
        }).RequireRateLimiting(RatePolicy);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var comment = await db.PublicationComments.SingleOrDefaultAsync(c => c.Id == id && c.UserId == User(user), ct);
            if (comment is null) return Results.NotFound();
            comment.IsDeleted = true; comment.Text = "";
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });

        group.MapPut("/comments/{id:guid}/like", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            var comment = await (from c in db.PublicationComments join p in Live(db) on c.PublicationId equals p.Id where c.Id == id select c).SingleOrDefaultAsync(ct);
            if (comment is null || comment.IsDeleted || comment.IsHidden) return Results.NotFound();
            if (comment.UserId == me) return Results.BadRequest(new { detail = "Não é possível curtir o próprio comentário." });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PublicationCommentLikes\" (\"CommentId\", \"UserId\", \"CreatedAt\") VALUES ({id}, {me}, {DateTimeOffset.UtcNow}) ON CONFLICT DO NOTHING", ct);
            return Results.NoContent();
        });
        group.MapDelete("/comments/{id:guid}/like", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            await db.PublicationCommentLikes.Where(l => l.CommentId == id && l.UserId == User(user)).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        group.MapPost("/comments/{id:guid}/{action}", async (Guid id, string action, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (action is not ("hide" or "unhide")) return Results.NotFound();
            var comment = await (from c in db.PublicationComments join p in db.Publications on c.PublicationId equals p.Id
                where c.Id == id && p.OwnerId == User(user) select c).SingleOrDefaultAsync(ct);
            if (comment is null) return Results.NotFound();
            comment.IsHidden = action == "hide"; comment.HiddenAt = comment.IsHidden ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });

        app.MapGet("/notes/{id:guid}/public-comments", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            if (!await db.Notes.AnyAsync(n => n.Id == id && n.OwnerId == User(user), ct)) return Results.NotFound();
            return Results.Ok(await (from c in db.PublicationComments.AsNoTracking()
                join p in Live(db) on c.PublicationId equals p.Id
                where p.NoteId == id orderby c.CreatedAt descending
                select new { c.Id, c.PublicationId, c.Text, c.ParentId, c.AnchorBlockId, c.AnchorQuote, c.AnchorStart, c.AnchorEnd, c.IsHidden, c.IsDeleted, c.CreatedAt }).Take(500).ToListAsync(ct));
        }).RequireAuthorization();

        var notifications = app.MapGroup("/me/notifications").RequireAuthorization();
        notifications.MapGet("", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string me = User(user);
            var query = from n in db.Notifications.AsNoTracking()
                join p in db.Publications.AsNoTracking() on n.PublicationId equals p.Id
                join profile in db.PublicProfiles.AsNoTracking() on p.OwnerId equals profile.UserId
                where n.UserId == me && p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public
                orderby n.CreatedAt descending
                select new { n.Id, n.Kind, n.PublicationId, n.CommentId, n.ActorUserId, n.CreatedAt, n.ReadAt,
                    path = "/p/" + profile.Handle + "/" + p.Slug };
            var unread = await query.Where(n => n.ReadAt == null).ToListAsync(ct);
            var recent = await query.Take(20).ToListAsync(ct);
            return Results.Ok(unread.Concat(recent).DistinctBy(n => n.Id).OrderByDescending(n => n.CreatedAt).ToList());
        });
        notifications.MapPost("/read", async (NotificationsReadInput input, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            var query = db.Notifications.Where(n => n.UserId == User(user) && n.ReadAt == null);
            if (input.Ids is not null) query = query.Where(n => input.Ids.Contains(n.Id));
            await query.ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
            return Results.NoContent();
        });
    }

    private static IQueryable<Publication> Live(NoteDbContext db) => db.Publications.Where(p => p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public && p.CurrentRevisionId != null);
    private static string User(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public static bool ValidAnchor(string? content, DiscussionAnchor anchor)
    {
        if (string.IsNullOrEmpty(anchor.BlockId) || anchor.BlockId.Length > 100 || string.IsNullOrEmpty(anchor.ExactQuote) || anchor.ExactQuote.Length > 500 || anchor.StartIndex < 0 || content is null) return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            foreach (var block in doc.RootElement.EnumerateArray())
            {
                if (!block.TryGetProperty("id", out var id) || id.GetString() != anchor.BlockId || !block.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String) continue;
                string text = value.GetString() ?? "";
                return anchor.StartIndex + anchor.ExactQuote.Length <= text.Length && text.Substring(anchor.StartIndex, anchor.ExactQuote.Length) == anchor.ExactQuote;
            }
        }
        catch (JsonException) { }
        return false;
    }
}
