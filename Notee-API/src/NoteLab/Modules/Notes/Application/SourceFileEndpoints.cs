using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public static class SourceFileEndpoints
{
    public static void MapSourceFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sources").RequireAuthorization().WithTags("Source files");
        group.MapGet("/{id:guid}/files", ListAsync);
        group.MapPost("/{id:guid}/files", UploadAsync).DisableAntiforgery();
        group.MapDelete("/{id:guid}/files/{archiveId:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(Guid id, NoteDbContext db, SourceArchiveStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        if (!await db.Sources.AnyAsync(s => s.Id == id && s.OwnerId == owner, ct)) return Results.NotFound();
        var rows = await db.SourceArchives.AsNoTracking().Where(a => a.OwnerId == owner && a.SourceId == id && a.Kind == "file").OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return Results.Ok(rows.Select(a => new SourceFileResponse(a.Id, a.ContentType, a.SizeBytes, a.Sha256, a.Basis, a.CreatedAt, storage.DownloadUrl(a))));
    }

    private static async Task<IResult> UploadAsync(Guid id, HttpRequest request, NoteDbContext db, SourceArchiveStorage storage,
        ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        string owner = Owner(user);
        Source? source = await db.Sources.SingleOrDefaultAsync(s => s.Id == id && s.OwnerId == owner, ct);
        if (source is null) return Results.NotFound();
        if (request.ContentLength > 11 * 1024 * 1024) return Results.Problem(statusCode: 413, title: "Arquivo excede o limite de 10 MB.");
        var bodyLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 11 * 1024 * 1024;
        IFormCollection form;
        try { form = await request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return Results.Problem(statusCode: 413, title: "Formulário excede o limite de upload."); }
        IFormFile? file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null) return Results.Problem(statusCode: 400, title: "Selecione um arquivo PDF ou texto.");
        try
        {
            await using Stream input = file.OpenReadStream();
            SourceArchive archive = await storage.StoreUploadAsync(owner, source, input, file.Length, file.FileName, file.ContentType, db, ct);
            List<SourceCitation> citations = await db.SourceCitations.Where(c => c.OwnerId == owner && c.SourceId == id).ToListAsync(ct);
            int dailyLimit = Math.Max(1, config.GetValue<int?>("Sources:ChecksPerDay") ?? 200);
            int used = await db.SourceChecks.CountAsync(c => c.OwnerId == owner && c.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), ct);
            foreach (SourceCitation citation in citations.OrderBy(c => c.CreatedAt).Take(Math.Max(0, dailyLimit - used)))
            {
                var check = new SourceCheck(owner, id, SourceCheckLayer.Citation, citation.Id);
                db.SourceChecks.Add(check);
                db.OutboxMessages.Add(OutboxEvents.SourceCheckRequested(check));
            }
            await db.SaveChangesAsync(ct);
            return Results.Created($"/sources/{id}/files/{archive.Id}",
                new SourceFileResponse(archive.Id, archive.ContentType, archive.SizeBytes, archive.Sha256, archive.Basis, archive.CreatedAt, storage.DownloadUrl(archive)));
        }
        catch (SourceFileException ex) { return Results.Problem(statusCode: ex.StatusCode, title: "Upload recusado", detail: ex.Message); }
    }

    private static async Task<IResult> DeleteAsync(Guid id, Guid archiveId, NoteDbContext db, SourceArchiveStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        string owner = Owner(user);
        SourceArchive? archive = await db.SourceArchives.SingleOrDefaultAsync(a => a.Id == archiveId && a.SourceId == id && a.OwnerId == owner, ct);
        if (archive is null) return Results.NotFound();
        await storage.DeleteAsync(archive, ct);
        db.SourceArchives.Remove(archive);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static string Owner(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
