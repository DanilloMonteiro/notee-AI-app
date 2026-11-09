using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NoteLab.Domain;

namespace NoteLab.Application;

public record ProfileRequest(string Handle, string DisplayName);
public record ProfileResponse(string Handle, string DisplayName);
public record CreatePublicationRequest(string Visibility, string? Slug);
public record UpdatePublicationRequest(string Visibility, long ExpectedVersion);

public record PublicationResponse(
    Guid Id,
    Guid NoteId,
    string NoteTitle,
    string Slug,
    string Visibility,
    string Status,
    long Version,
    long? PublishedNoteVersion,
    long CurrentNoteVersion,
    DateTimeOffset? PublishedAt,
    string? ShareToken,
    string? PublicPath,
    string? SharedPath);

public record PublicAuthor(string Handle, string DisplayName);
public record PublicNoteResponse(string Title, string Content, DateTimeOffset PublishedAt, string Slug, PublicAuthor? Author);
public record PublicNoteSummary(string Title, string Slug, DateTimeOffset PublishedAt, PublicAuthor Author);
public record PublicProfileResponse(PublicAuthor Author, List<PublicNoteSummary> Notes);

/// <summary>
/// Publicação revisada (M08): snapshots imutáveis com visibilidade Private/Unlisted/Public,
/// revogação imediata, perfil e biblioteca somente leitura (sem rede social).
/// </summary>
public static class PublishingEndpoints
{
    public static void MapPublishingEndpoints(this IEndpointRouteBuilder app)
    {
        MapAuthorEndpoints(app);
        MapPublicEndpoints(app);
    }

    private static void MapAuthorEndpoints(IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me").RequireAuthorization().WithTags("Publishing");

        me.MapGet("/profile", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            PublicProfile? p = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Owner(user), ct);
            return p is null ? Results.NoContent() : Results.Ok(new ProfileResponse(p.Handle, p.DisplayName));
        });

        me.MapPut("/profile", async (ProfileRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            PublicProfile? profile = await db.PublicProfiles.SingleOrDefaultAsync(x => x.UserId == owner, ct);
            if (profile is null)
            {
                profile = new PublicProfile(owner, request.Handle, request.DisplayName);
                db.PublicProfiles.Add(profile);
            }
            else
            {
                profile.Update(request.Handle, request.DisplayName);
            }

            if (await db.PublicProfiles.AnyAsync(x => x.Handle == profile.Handle && x.UserId != owner, ct))
            {
                return Problem(409, "Este identificador já está em uso.");
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Problem(409, "Este identificador já está em uso.");
            }

            return Results.Ok(new ProfileResponse(profile.Handle, profile.DisplayName));
        });

        var pubs = app.MapGroup("").RequireAuthorization().WithTags("Publishing");

        pubs.MapGet("/publications", async (ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
            Results.Ok(await ListAsync(db, Owner(user), null, ct)));

        pubs.MapGet("/notes/{noteId:guid}/publications", async (Guid noteId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct))
            {
                return Results.NotFound();
            }

            return Results.Ok(await ListAsync(db, owner, noteId, ct));
        });

        pubs.MapPost("/notes/{noteId:guid}/publications", async (Guid noteId, CreatePublicationRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Note? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == owner, ct);
            if (note is null)
            {
                return Results.NotFound();
            }

            if (note.Status == NoteStatus.Trashed)
            {
                return Problem(400, "Notas na lixeira não podem ser publicadas.");
            }

            if (!TryVisibility(request.Visibility, out PublicationVisibility visibility))
            {
                return Problem(400, "Visibilidade deve ser private, unlisted ou public.");
            }

            if (visibility == PublicationVisibility.Public && !await db.PublicProfiles.AnyAsync(p => p.UserId == owner, ct))
            {
                return Problem(400, "Defina seu perfil público (identificador) antes de publicar como pública.");
            }

            var publication = new Publication(owner, note.Id, string.IsNullOrWhiteSpace(request.Slug) ? note.Title.Value : request.Slug, visibility);
            if (await db.Publications.AnyAsync(p => p.OwnerId == owner && p.Slug == publication.Slug && p.Status == PublicationStatus.Published, ct))
            {
                return Problem(409, "Você já tem uma publicação ativa com este endereço.");
            }

            var revision = new PublicationRevision(publication.Id, note);
            publication.SetRevision(revision.Id);
            db.Publications.Add(publication);
            db.PublicationRevisions.Add(revision);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/publications/{publication.Id}", (await ListAsync(db, owner, null, ct, publication.Id)).Single());
        });

        // Publica o estado ATUAL da nota como nova revisão (a anterior continua no histórico).
        pubs.MapPost("/publications/{id:guid}/revisions", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Publication? publication = await db.Publications.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == owner, ct);
            if (publication is null)
            {
                return Results.NotFound();
            }

            Note? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == publication.NoteId && n.OwnerId == owner, ct);
            if (note is null || note.Status == NoteStatus.Trashed)
            {
                return Problem(400, "A nota não está disponível para publicação.");
            }

            var revision = new PublicationRevision(publication.Id, note);
            publication.SetRevision(revision.Id);
            db.PublicationRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            return Results.Ok((await ListAsync(db, owner, null, ct, id)).Single());
        });

        pubs.MapPatch("/publications/{id:guid}", async (Guid id, UpdatePublicationRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Publication? publication = await db.Publications.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == owner, ct);
            if (publication is null)
            {
                return Results.NotFound();
            }

            if (publication.Version != request.ExpectedVersion)
            {
                return Problem(409, "A publicação foi alterada em outro lugar. Recarregue.");
            }

            if (!TryVisibility(request.Visibility, out PublicationVisibility visibility))
            {
                return Problem(400, "Visibilidade deve ser private, unlisted ou public.");
            }

            if (visibility == PublicationVisibility.Public && !await db.PublicProfiles.AnyAsync(p => p.UserId == owner, ct))
            {
                return Problem(400, "Defina seu perfil público antes de tornar a publicação pública.");
            }

            publication.ChangeVisibility(visibility);
            await db.SaveChangesAsync(ct);
            return Results.Ok((await ListAsync(db, owner, null, ct, id)).Single());
        });

        pubs.MapPost("/publications/{id:guid}/rotate-token", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Publication? publication = await db.Publications.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == owner, ct);
            if (publication is null)
            {
                return Results.NotFound();
            }

            publication.RotateToken();
            await db.SaveChangesAsync(ct);
            return Results.Ok((await ListAsync(db, owner, null, ct, id)).Single());
        });

        // Revogar tira do ar imediatamente (link público e secreto deixam de funcionar).
        pubs.MapDelete("/publications/{id:guid}", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            Publication? publication = await db.Publications.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == Owner(user), ct);
            if (publication is null)
            {
                return Results.NotFound();
            }

            if (publication.Status == PublicationStatus.Revoked)
            {
                return Results.NoContent();
            }

            publication.Revoke();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Prévia do autor (qualquer visibilidade).
        pubs.MapGet("/publications/{id:guid}/preview", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            Publication? publication = await db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == Owner(user), ct);
            return publication is null ? Results.NotFound() : await PublicViewAsync(db, publication, ct);
        });
    }

    private static void MapPublicEndpoints(IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("").AllowAnonymous().WithTags("PublicReading");

        // Biblioteca de leitura: publicações públicas mais recentes.
        pub.MapGet("/public/notes", async (NoteDbContext db, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            var rows = await (from p in db.Publications.AsNoTracking()
                              join r in db.PublicationRevisions.AsNoTracking() on p.CurrentRevisionId equals r.Id
                              join prof in db.PublicProfiles.AsNoTracking() on p.OwnerId equals prof.UserId
                              where p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public
                              orderby r.PublishedAt descending, p.Id
                              select new { r.Title, p.Slug, r.PublishedAt, prof.Handle, prof.DisplayName })
                .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Results.Ok(rows.Select(x => new PublicNoteSummary(x.Title, x.Slug, x.PublishedAt, new PublicAuthor(x.Handle, x.DisplayName))));
        });

        pub.MapGet("/public/profiles/{handle}", async (string handle, NoteDbContext db, CancellationToken ct) =>
        {
            PublicProfile? profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            if (profile is null)
            {
                return Results.NotFound();
            }

            var author = new PublicAuthor(profile.Handle, profile.DisplayName);
            var notes = await (from p in db.Publications.AsNoTracking()
                               join r in db.PublicationRevisions.AsNoTracking() on p.CurrentRevisionId equals r.Id
                               where p.OwnerId == profile.UserId && p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public
                               orderby r.PublishedAt descending
                               select new { r.Title, p.Slug, r.PublishedAt }).Take(100).ToListAsync(ct);
            return Results.Ok(new PublicProfileResponse(author, notes.Select(n => new PublicNoteSummary(n.Title, n.Slug, n.PublishedAt, author)).ToList()));
        });

        pub.MapGet("/public/notes/{handle}/{slug}", async (string handle, string slug, NoteDbContext db, CancellationToken ct) =>
        {
            PublicProfile? profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Handle == handle.ToLower(), ct);
            Publication? publication = profile is null
                ? null
                : await db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == profile.UserId && p.Slug == slug.ToLower()
                    && p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public, ct);
            return publication is null ? Results.NotFound() : await PublicViewAsync(db, publication, ct);
        });

        // Link secreto: funciona para Unlisted e Public enquanto não revogado.
        pub.MapGet("/shared/{token}", async (string token, NoteDbContext db, CancellationToken ct) =>
        {
            Publication? publication = await db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.ShareToken == token
                && p.Status == PublicationStatus.Published && p.Visibility != PublicationVisibility.Private, ct);
            return publication is null ? Results.NotFound() : await PublicViewAsync(db, publication, ct);
        });
    }

    private static async Task<IResult> PublicViewAsync(NoteDbContext db, Publication publication, CancellationToken ct)
    {
        PublicationRevision? revision = await db.PublicationRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == publication.CurrentRevisionId, ct);
        if (revision is null)
        {
            return Results.NotFound();
        }

        PublicProfile? profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == publication.OwnerId, ct);
        return Results.Ok(new PublicNoteResponse(revision.Title, revision.Content ?? string.Empty, revision.PublishedAt, publication.Slug,
            profile is null ? null : new PublicAuthor(profile.Handle, profile.DisplayName)));
    }

    private static async Task<List<PublicationResponse>> ListAsync(NoteDbContext db, string owner, Guid? noteId, CancellationToken ct, Guid? publicationId = null)
    {
        string? handle = await db.PublicProfiles.AsNoTracking().Where(p => p.UserId == owner).Select(p => p.Handle).SingleOrDefaultAsync(ct);
        var rows = await (from p in db.Publications.AsNoTracking()
                          join n in db.Notes.AsNoTracking() on p.NoteId equals n.Id
                          join r in db.PublicationRevisions.AsNoTracking() on p.CurrentRevisionId equals r.Id into rr
                          from r in rr.DefaultIfEmpty()
                          where p.OwnerId == owner
                                && (noteId == null || p.NoteId == noteId)
                                && (publicationId == null || p.Id == publicationId)
                          orderby p.CreatedAt descending
                          select new { p, n.Title, n.Version, RevVersion = (long?)r.NoteVersion, PublishedAt = (DateTimeOffset?)r.PublishedAt })
            .ToListAsync(ct);

        return rows.Select(x =>
        {
            bool live = x.p.Status == PublicationStatus.Published;
            return new PublicationResponse(x.p.Id, x.p.NoteId, x.Title.Value, x.p.Slug, x.p.Visibility.ToString().ToLowerInvariant(),
                x.p.Status.ToString(), x.p.Version, x.RevVersion, x.Version, x.PublishedAt,
                live && x.p.Visibility != PublicationVisibility.Private ? x.p.ShareToken : null,
                live && x.p.Visibility == PublicationVisibility.Public && handle is not null ? $"/p/{handle}/{x.p.Slug}" : null,
                live && x.p.Visibility != PublicationVisibility.Private ? $"/s/{x.p.ShareToken}" : null);
        }).ToList();
    }

    private static bool TryVisibility(string? value, out PublicationVisibility visibility) =>
        Enum.TryParse(value, ignoreCase: true, out visibility) && Enum.IsDefined(visibility) && !int.TryParse(value, out _);

    private static IResult Problem(int status, string detail) =>
        Results.Problem(statusCode: status, title: status == 409 ? "Conflito" : "Erro de validação", detail: detail);

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
}
