using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using UglyToad.PdfPig;

namespace NoteLab.Application;

public sealed record SourceFileResponse(Guid Id, string ContentType, long SizeBytes, string Sha256, string Basis,
    DateTimeOffset CreatedAt, string DownloadUrl);

public sealed class SourceArchiveStorage(IAmazonS3 s3, IConfiguration config)
{
    private const long PerFileLimit = 10 * 1024 * 1024;
    private const long OwnerLimit = 200L * 1024 * 1024;
    private string Bucket => config["Sources:S3:Bucket"] ?? "notee-source-archives";

    public async Task<SourceArchive> StoreUploadAsync(string owner, Source source, Stream input, long length, string fileName,
        string contentType, NoteDbContext db, CancellationToken ct)
    {
        if (length <= 0 || length > PerFileLimit) throw new SourceFileException(413, "O arquivo deve ter até 10 MB.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({owner})::bigint)", ct);
        long used = await db.SourceArchives.Where(a => a.OwnerId == owner).SumAsync(a => (long?)a.SizeBytes, ct) ?? 0;
        if (used + length > OwnerLimit) throw new SourceFileException(409, "A cota de arquivos de 200 MB por conta seria excedida.");
        await using var buffer = new MemoryStream((int)length);
        await input.CopyToAsync(buffer, ct);
        byte[] bytes = buffer.ToArray();
        if (bytes.Length != length || bytes.Length > PerFileLimit) throw new SourceFileException(413, "O arquivo excede 10 MB.");
        string media = ValidateFile(bytes, fileName, contentType);
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        List<ExtractedPage> pages = Extract(bytes, media);
        string root = $"owners/{owner}/sources/{source.Id}/{Guid.NewGuid():N}";
        string storageKey = root + (media == "application/pdf" ? ".pdf" : ".txt");
        string textKey = root + ".text.json";
        await EnsureBucketAsync(ct);
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = storageKey, InputStream = new MemoryStream(bytes), ContentType = media, AutoCloseStream = true }, ct);
            byte[] text = JsonSerializer.SerializeToUtf8Bytes(pages);
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = textKey, InputStream = new MemoryStream(text), ContentType = "application/json", AutoCloseStream = true }, ct);
        }
        catch
        {
            await DeleteKeyQuietlyAsync(storageKey, ct);
            await DeleteKeyQuietlyAsync(textKey, ct);
            throw;
        }
        var archive = new SourceArchive(owner, source.Id, storageKey, textKey, media, bytes.Length, sha);
        db.SourceArchives.Add(archive);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch
        {
            await DeleteKeyQuietlyAsync(storageKey, ct); await DeleteKeyQuietlyAsync(textKey, ct); throw;
        }
        return archive;
    }

    public async Task<SourceArchive> StoreOpenAccessCopyAsync(string owner, Source source, byte[] bytes, string originUrl,
        string license, NoteDbContext db, CancellationToken ct)
    {
        if (bytes.Length == 0 || bytes.Length > PerFileLimit) throw new SourceFileException(413, "A cópia de acesso aberto deve ter até 10 MB.");
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-") throw new SourceFileException(400, "O provedor não retornou um PDF válido.");
        string? basis = SourcePageMetadata.NormalizeCopyLicense(license);
        if (basis is null) throw new SourceFileException(400, "A licença não autoriza cópia automática.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({owner})::bigint)", ct);
        long used = await db.SourceArchives.Where(a => a.OwnerId == owner).SumAsync(a => (long?)a.SizeBytes, ct) ?? 0;
        if (used + bytes.Length > OwnerLimit) throw new SourceFileException(409, "A cota de arquivos de 200 MB por conta seria excedida.");
        string root = $"owners/{owner}/sources/{source.Id}/{Guid.NewGuid():N}";
        string storageKey = root + ".pdf"; string textKey = root + ".text.json";
        List<ExtractedPage> pages = Extract(bytes, "application/pdf");
        await EnsureBucketAsync(ct);
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = storageKey, InputStream = new MemoryStream(bytes), ContentType = "application/pdf", AutoCloseStream = true }, ct);
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = textKey, InputStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(pages)), ContentType = "application/json", AutoCloseStream = true }, ct);
            var archive = new SourceArchive(owner, source.Id, storageKey, textKey, "application/pdf", bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), basis);
            archive.SetSnapshot(originUrl, null);
            db.SourceArchives.Add(archive);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return archive;
        }
        catch
        {
            await DeleteKeyQuietlyAsync(storageKey, ct); await DeleteKeyQuietlyAsync(textKey, ct); throw;
        }
    }

    public async Task<SourceArchive> StoreLicensedTextAsync(string owner, Source source, string text, string originUrl,
        string license, NoteDbContext db, CancellationToken ct)
    {
        string? basis = SourcePageMetadata.NormalizeCopyLicense(license);
        if (basis is null || basis == "public-domain") throw new SourceFileException(400, "A página não declarou uma licença Creative Commons reconhecida.");
        string normalizedText = string.Join("\n", text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(normalizedText);
        if (bytes.Length == 0 || bytes.Length > PerFileLimit) throw new SourceFileException(413, "O texto arquivado deve ter até 10 MB.");
        List<ExtractedPage> pages = [new ExtractedPage(1, normalizedText)];
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({owner})::bigint)", ct);
        long used = await db.SourceArchives.Where(a => a.OwnerId == owner).SumAsync(a => (long?)a.SizeBytes, ct) ?? 0;
        if (used + bytes.Length > OwnerLimit) throw new SourceFileException(409, "A cota de arquivos de 200 MB por conta seria excedida.");
        string root = $"owners/{owner}/sources/{source.Id}/{Guid.NewGuid():N}";
        string storageKey = root + ".txt"; string textKey = root + ".text.json";
        await EnsureBucketAsync(ct);
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = storageKey, InputStream = new MemoryStream(bytes), ContentType = "text/plain; charset=utf-8", AutoCloseStream = true }, ct);
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = textKey, InputStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(pages)), ContentType = "application/json", AutoCloseStream = true }, ct);
            var archive = new SourceArchive(owner, source.Id, storageKey, textKey, "text/plain", bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), basis);
            archive.SetSnapshot(originUrl, null);
            db.SourceArchives.Add(archive);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return archive;
        }
        catch
        {
            await DeleteKeyQuietlyAsync(storageKey, ct); await DeleteKeyQuietlyAsync(textKey, ct); throw;
        }
    }

    public string DownloadUrl(SourceArchive archive) => archive.Kind == "snapshot" ? archive.SnapshotUrl ?? "" : s3.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = Bucket, Key = archive.StorageKey, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.AddMinutes(5)
    });

    public async Task<List<ExtractedPage>> ReadTextAsync(SourceArchive archive, CancellationToken ct)
    {
        if (archive.TextKey is null) return [];
        using GetObjectResponse response = await s3.GetObjectAsync(Bucket, archive.TextKey, ct);
        return await JsonSerializer.DeserializeAsync<List<ExtractedPage>>(response.ResponseStream, cancellationToken: ct) ?? [];
    }

    public async Task DeleteAsync(SourceArchive archive, CancellationToken ct)
    {
        if (archive.Kind == "snapshot") return;
        await s3.DeleteObjectAsync(Bucket, archive.StorageKey, ct);
        if (archive.TextKey is not null) await s3.DeleteObjectAsync(Bucket, archive.TextKey, ct);
    }

    public async Task StoreSnapshotAsync(NoteDbContext db, string owner, Guid sourceId, string originUrl, string snapshotUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(snapshotUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase) || !uri.AbsolutePath.StartsWith("/web/", StringComparison.Ordinal))
            throw new SourceFileException(400, "O link de snapshot não pertence ao Internet Archive.");
        bool exists = await db.SourceArchives.AnyAsync(a => a.OwnerId == owner && a.SourceId == sourceId && a.Kind == "snapshot" && a.SnapshotUrl == snapshotUrl, ct);
        if (exists) return;
        string sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotUrl))).ToLowerInvariant();
        var snapshot = new SourceArchive(owner, sourceId, "", null, "application/x-wayback-snapshot", 0, sha, "snapshot", "snapshot");
        snapshot.SetSnapshot(originUrl, snapshotUrl);
        db.SourceArchives.Add(snapshot);
    }

    public async Task DeleteSourceAsync(NoteDbContext db, string owner, Guid sourceId, CancellationToken ct)
    {
        List<SourceArchive> rows = await db.SourceArchives.Where(a => a.OwnerId == owner && a.SourceId == sourceId).ToListAsync(ct);
        foreach (SourceArchive row in rows) await DeleteAsync(row, ct);
        db.SourceArchives.RemoveRange(rows);
    }

    public async Task DeleteOwnerAsync(NoteDbContext db, string owner, CancellationToken ct)
    {
        List<SourceArchive> rows = await db.SourceArchives.Where(a => a.OwnerId == owner).ToListAsync(ct);
        foreach (SourceArchive row in rows) await DeleteAsync(row, ct);
        db.SourceArchives.RemoveRange(rows);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        try { await s3.GetBucketLocationAsync(Bucket, ct); }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchBucket")
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket }, ct);
        }
    }

    private async Task DeleteKeyQuietlyAsync(string key, CancellationToken ct)
    {
        try { await s3.DeleteObjectAsync(Bucket, key, ct); }
        catch (AmazonS3Exception) { }
    }

    private static string ValidateFile(byte[] bytes, string name, string contentType)
    {
        bool pdf = bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
        if (pdf && (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))) return "application/pdf";
        string ext = Path.GetExtension(name);
        if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) && !bytes.Contains((byte)0))
        {
            try { _ = new UTF8Encoding(false, true).GetString(bytes); return "text/plain"; }
            catch (DecoderFallbackException) { }
        }
        throw new SourceFileException(400, "Envie um PDF válido ou um arquivo .txt em UTF-8.");
    }

    private static List<ExtractedPage> Extract(byte[] bytes, string contentType)
    {
        if (contentType == "text/plain") return [new ExtractedPage(1, new UTF8Encoding(false, true).GetString(bytes))];
        try
        {
            using PdfDocument document = PdfDocument.Open(bytes);
            List<ExtractedPage> pages = document.GetPages().Select(page => new ExtractedPage(page.Number, page.Text)).ToList();
            if (pages.Count == 0) throw new SourceFileException(400, "O PDF não contém páginas legíveis.");
            return pages;
        }
        catch (SourceFileException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            throw new SourceFileException(400, "O PDF está corrompido ou não pôde ser lido.");
        }
    }
}

public sealed class SourceFileException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
