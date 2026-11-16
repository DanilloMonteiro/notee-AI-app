using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public sealed record PublishedCitation(Guid Id, string Kind, string Title, List<string> Authors, int? Year,
    string? Venue, string? Language, string? Doi, string? Isbn, string? Url, string? SnapshotUrl, string License,
    string? BlockId, string? ExactQuote, int? StartIndex, int? EndIndex, string LocatorKind,
    string? LocatorText, int? PageStart, int? PageEnd, int? TimeStartSec, int? TimeEndSec,
    string VerificationStatus);

public static class SourceCitationSnapshot
{
    public static async Task<string> CaptureAsync(NoteDbContext db, Guid noteId, string ownerId, CancellationToken ct)
    {
        var rows = await (from citation in db.SourceCitations.AsNoTracking()
                          join source in db.Sources.AsNoTracking() on citation.SourceId equals source.Id
                          where citation.NoteId == noteId && citation.OwnerId == ownerId && source.OwnerId == ownerId
                          orderby source.Title, citation.CreatedAt
                          select new { Citation = citation, Source = source }).ToListAsync(ct);

        Guid[] sourceIds = rows.Select(row => row.Source.Id).Distinct().ToArray();
        List<SourceCheck> checks = await db.SourceChecks.AsNoTracking().Where(check => check.OwnerId == ownerId && sourceIds.Contains(check.SourceId))
            .OrderByDescending(check => check.CreatedAt).ToListAsync(ct);
        Dictionary<Guid, string> statuses = checks.GroupBy(check => new { check.SourceId, check.Layer }).Select(group => group.First())
            .GroupBy(check => check.SourceId).ToDictionary(group => group.Key,
                group => group.OrderByDescending(check => StatusSeverity(check.Status)).First().Status.ToString().ToLowerInvariant());
        List<SourceArchive> snapshots = await db.SourceArchives.AsNoTracking()
            .Where(archive => archive.OwnerId == ownerId && archive.Kind == "snapshot" && sourceIds.Contains(archive.SourceId) && archive.SnapshotUrl != null)
            .OrderByDescending(archive => archive.CreatedAt).ToListAsync(ct);
        Dictionary<Guid, string> snapshotUrls = snapshots.GroupBy(archive => archive.SourceId)
            .ToDictionary(group => group.Key, group => group.First().SnapshotUrl!);

        List<PublishedCitation> snapshot = rows.Select(row =>
        {
            SourceCitation c = row.Citation;
            Source s = row.Source;
            return new PublishedCitation(c.Id, s.Kind.ToString().ToLowerInvariant(), s.Title,
                s.Authors.ToList(), s.Year, s.Venue, s.Language, s.Doi, s.Isbn, s.Url,
                snapshotUrls.GetValueOrDefault(s.Id), s.License,
                c.BlockId, c.ExactQuote, c.StartIndex, c.EndIndex, c.LocatorKind.ToString().ToLowerInvariant(),
                c.LocatorText, c.PageStart, c.PageEnd, c.TimeStartSec, c.TimeEndSec, statuses.GetValueOrDefault(s.Id, "unverified"));
        }).ToList();
        return JsonSerializer.Serialize(snapshot);
    }

    private static int StatusSeverity(SourceCheckStatus status) => status switch
    {
        SourceCheckStatus.NotFound or SourceCheckStatus.Mismatch => 8,
        SourceCheckStatus.QuoteNotFound => 7,
        SourceCheckStatus.FoundWithDifferences => 6,
        SourceCheckStatus.Failed => 5,
        SourceCheckStatus.Running => 4,
        SourceCheckStatus.Pending => 3,
        SourceCheckStatus.Unverifiable => 2,
        _ => 1
    };

    public static List<PublishedCitation> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<PublishedCitation>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
