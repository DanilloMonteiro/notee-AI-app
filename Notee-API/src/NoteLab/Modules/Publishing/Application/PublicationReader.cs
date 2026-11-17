using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

/// <summary>
/// Leitura anônima de publicações: única regra de visibilidade usada pelas páginas públicas
/// e pela criação de nota a partir de outra nota (ADR-044). Sempre lê o snapshot, nunca o rascunho.
/// </summary>
public static class PublicationReader
{
    /// <summary>/p/{handle}/{slug}: somente publicações ativas e públicas.</summary>
    public static async Task<Publication?> FindPublicAsync(NoteDbContext db, string handle, string slug, CancellationToken ct)
    {
        string lowerHandle = handle.ToLower();
        PublicProfile? profile = await db.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Handle == lowerHandle, ct);
        if (profile is null)
        {
            return null;
        }

        string lowerSlug = slug.ToLower();
        return await db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == profile.UserId && p.Slug == lowerSlug
            && p.Status == PublicationStatus.Published && p.Visibility == PublicationVisibility.Public, ct);
    }

    /// <summary>/s/{token}: link secreto, vale para Unlisted e Public enquanto não revogado.</summary>
    public static Task<Publication?> FindSharedAsync(NoteDbContext db, string token, CancellationToken ct) =>
        db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.ShareToken == token
            && p.Status == PublicationStatus.Published && p.Visibility != PublicationVisibility.Private, ct);

    public static Task<PublicationRevision?> CurrentRevisionAsync(NoteDbContext db, Publication publication, CancellationToken ct) =>
        db.PublicationRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == publication.CurrentRevisionId, ct);
}
