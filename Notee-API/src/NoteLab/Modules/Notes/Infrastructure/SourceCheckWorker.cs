using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

/// <summary>Checagem automática durável de existência e metadados; claims usam SKIP LOCKED e lease.</summary>
public sealed class SourceCheckWorker(IServiceScopeFactory scopes, SafeHttpClient http, SourceArchiveStorage archives, IConfiguration config,
    ILogger<SourceCheckWorker> logger) : BackgroundService
{
    private const int BatchSize = 3;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}"[..48];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int count = await ProcessBatchAsync(stoppingToken);
                if (count == 0) await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning("Worker de fontes falhou: {Error}", ex.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset until = now + Lease;
        List<Claimed> claimed = await db.Database.SqlQuery<Claimed>($"""
            UPDATE "SourceChecks" SET "Status" = 'Running', "LeaseOwner" = {_workerId}, "LeaseExpiresAt" = {until},
                "FencingToken" = "FencingToken" + 1, "Attempts" = "Attempts" + 1
            WHERE "Id" IN (SELECT "Id" FROM "SourceChecks" WHERE ("Status" = 'Pending' AND "NextAttemptAt" <= {now}) OR ("Status" = 'Running' AND "LeaseExpiresAt" < {now})
                ORDER BY "CreatedAt" LIMIT {BatchSize} FOR UPDATE SKIP LOCKED)
            RETURNING "Id", "FencingToken"
            """).ToListAsync(ct);
        foreach (Claimed lease in claimed)
        {
            SourceCheck? check = await db.SourceChecks.SingleOrDefaultAsync(c => c.Id == lease.Id && c.FencingToken == lease.FencingToken, ct);
            Source? source = check is null ? null : await db.Sources.SingleOrDefaultAsync(s => s.Id == check.SourceId && s.OwnerId == check.OwnerId, ct);
            if (check is null || source is null) continue;
            try
            {
                SourceCheckResult result = check.Layer == SourceCheckLayer.Citation
                    ? await VerifyCitationAsync(db, check, source, ct)
                    : await VerifyAsync(db, source, ct);
                check.Complete(result.Status, result.Summary, result.Arguments, result.Provider);
                if (check.Layer == SourceCheckLayer.Existence && !string.IsNullOrWhiteSpace(source.Url))
                {
                    List<SourceCheckArgument> args = result.Arguments.ToList();
                    await AddWaybackEvidenceAsync(db, source, source.Url, args, ct);
                    if (args.Count != result.Arguments.Count) check.Complete(result.Status, result.Summary, args, result.Provider);
                }
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException)
                {
                    logger.LogInformation("Source check {CheckId} descartado por fencing", check.Id);
                    db.ChangeTracker.Clear();
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                bool final = check.Attempts >= 5;
                if (final) check.Complete(SourceCheckStatus.Failed, "Não foi possível verificar após várias tentativas.", Array.Empty<object>(), "source-check");
                else check.Retry($"Falha temporária ({ex.GetType().Name}); nova tentativa agendada.");
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
            }
        }
        return claimed.Count;
    }

    private sealed class Claimed { public Guid Id { get; set; } public long FencingToken { get; set; } }

    private async Task<SourceCheckResult> VerifyCitationAsync(NoteDbContext db, SourceCheck check, Source source, CancellationToken ct)
    {
        SourceCitation? citation = check.CitationId is null ? null : await db.SourceCitations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == check.CitationId && c.OwnerId == check.OwnerId && c.SourceId == source.Id, ct);
        if (citation is null) return new(SourceCheckStatus.Unverifiable, "A citação não está mais disponível.", Array.Empty<SourceCheckArgument>(), "archive");
        SourceArchive? archive = await db.SourceArchives.AsNoTracking().Where(a => a.OwnerId == check.OwnerId && a.SourceId == source.Id && a.Kind == "file")
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        if (archive is null) return new(SourceCheckStatus.Unverifiable, "Não há texto arquivado para conferir o trecho.", Array.Empty<SourceCheckArgument>(), "archive");
        string quote = citation.SourceQuote ?? citation.ExactQuote ?? "";
        if (string.IsNullOrWhiteSpace(quote)) return new(SourceCheckStatus.Unverifiable, "Informe o trecho da fonte para fazer a conferência.", Array.Empty<SourceCheckArgument>(), "archive");
        List<ExtractedPage> pages = await archives.ReadTextAsync(archive, ct);
        return SourceQuoteMatcher.Compare(pages, quote, citation.PageStart, citation.PageEnd, source.Url);
    }

    private async Task<SourceCheckResult> VerifyAsync(NoteDbContext db, Source source, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(source.Doi))
        {
            string baseUrl = config["Sources:Providers:CrossrefBaseUrl"] ?? "https://api.crossref.org";
            string email = config["Sources:ContactEmail"] ?? "";
            string endpoint = $"{baseUrl.TrimEnd('/')}/works/{Uri.EscapeDataString(source.Doi)}" + (string.IsNullOrWhiteSpace(email) ? "" : $"?mailto={Uri.EscapeDataString(email)}");
            SafeHttpResponse response = await http.GetAsync(endpoint, ct, allowJson: true);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(SourceCheckStatus.NotFound, "DOI não encontrado no Crossref.", [new("DOI", source.Doi, endpoint)], "crossref");
            using JsonDocument json = JsonDocument.Parse(response.Body);
            JsonElement message = json.RootElement.GetProperty("message");
            string? title = First(message, "title");
            int? year = null;
            foreach (string field in new[] { "published-print", "published-online", "created" })
                if (message.TryGetProperty(field, out JsonElement date) && date.TryGetProperty("date-parts", out JsonElement parts) && parts[0][0].TryGetInt32(out int y)) { year = y; break; }
            List<string> authors = message.TryGetProperty("author", out JsonElement authorRows) && authorRows.ValueKind == JsonValueKind.Array
                ? authorRows.EnumerateArray().Select(author => string.Join(" ", new[] { GetString(author, "given"), GetString(author, "family") }
                    .Where(value => !string.IsNullOrWhiteSpace(value)))).Where(value => value.Length > 0).Take(20).ToList() : [];
            List<SourceCheckArgument> differences = SourceMetadataComparer.Compare(source.Title, source.Year, title, year, endpoint, source.Authors, authors);
            List<SourceCheckArgument> args = differences.ToList();
            if (differences.Count == 0) args.Add(new("Registro Crossref", title ?? source.Doi!, endpoint));
            await TryOpenAccessCopyAsync(db, source, args, ct);
            return differences.Count == 0
                ? new(SourceCheckStatus.Confirmed, "DOI encontrado; os metadados informados não apresentam divergências detectadas.", args, "crossref")
                : new(SourceCheckStatus.FoundWithDifferences, "DOI encontrado com divergências de metadados.", args, "crossref");
        }
        if (!string.IsNullOrWhiteSpace(source.Isbn))
        {
            string endpoint = $"{(config["Sources:Providers:OpenLibraryBaseUrl"] ?? "https://openlibrary.org").TrimEnd('/')}/isbn/{source.Isbn}.json";
            SafeHttpResponse response = await http.GetAsync(endpoint, ct, allowJson: true);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(SourceCheckStatus.NotFound, "ISBN não encontrado na Open Library.", [new("ISBN", source.Isbn, endpoint)], "openlibrary");
            using JsonDocument json = JsonDocument.Parse(response.Body);
            string? title = json.RootElement.TryGetProperty("title", out JsonElement t) ? t.GetString() : null;
            string? published = GetString(json.RootElement, "publish_date");
            int? year = int.TryParse(System.Text.RegularExpressions.Regex.Match(published ?? "", @"\b(?:19|20)\d{2}\b").Value, out int parsedYear) ? parsedYear : null;
            List<string> authors = json.RootElement.TryGetProperty("authors", out JsonElement authorRows) && authorRows.ValueKind == JsonValueKind.Array
                ? authorRows.EnumerateArray().Select(author => GetString(author, "name")).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Take(20).ToList() : [];
            List<SourceCheckArgument> args = SourceMetadataComparer.Compare(source.Title, source.Year, title, year, endpoint, source.Authors, authors);
            return args.Count == 0
                ? new(SourceCheckStatus.Confirmed, "ISBN encontrado; não foi detectada divergência de título.", [new("Registro Open Library", title ?? source.Isbn, endpoint)], "openlibrary")
                : new(SourceCheckStatus.FoundWithDifferences, "ISBN encontrado com divergência de título.", args, "openlibrary");
        }
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            SafeHttpResponse response = await http.GetAsync(source.Url, ct);
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
                return new(SourceCheckStatus.NotFound, $"A URL respondeu HTTP {(int)response.StatusCode}.", [new("URL", source.Url, source.Url)], "http");
            var args = new List<SourceCheckArgument>();
            if ((int)response.StatusCode < 400 && (response.ContentType == "text/html" || response.ContentType == "application/xhtml+xml"))
                await AddLicensedPageCopyAsync(db, source, response, args, ct);
            if ((int)response.StatusCode < 400 && (response.ContentType == "text/html" || response.ContentType == "application/xhtml+xml"))
            {
                List<SourceCheckArgument> differences = SourceMetadataComparer.Compare(source.Title, source.Year,
                    SourcePageMetadata.PageTitle(response.ReadText()), SourcePageMetadata.PublicationYear(response.ReadText()),
                    response.FinalUri.ToString(), source.Authors, SourcePageMetadata.Authors(response.ReadText()));
                args.InsertRange(0, differences);
            }
            args.Add(new("Resposta HTTP", ((int)response.StatusCode).ToString(), source.Url));
            return (int)response.StatusCode < 400
                ? new(args.Any(argument => argument.Point is "Título divergente" or "Ano divergente" or "Autor divergente")
                    ? SourceCheckStatus.FoundWithDifferences : SourceCheckStatus.Confirmed,
                    args.Any(argument => argument.Point is "Título divergente" or "Ano divergente" or "Autor divergente")
                    ? "A URL respondeu com sucesso, mas há diferenças entre os metadados cadastrados e a página."
                    : "A URL respondeu com sucesso; isso não confirma o conteúdo publicado.", args, "http")
                : new(SourceCheckStatus.FoundWithDifferences, $"A URL respondeu HTTP {(int)response.StatusCode}.", args, "http");
        }
        return new(SourceCheckStatus.Unverifiable, "Sem DOI, ISBN ou URL verificável.", [], null);
    }

    private async Task TryOpenAccessCopyAsync(NoteDbContext db, Source source, List<SourceCheckArgument> arguments, CancellationToken ct)
    {
        if (await db.SourceArchives.AnyAsync(a => a.OwnerId == source.OwnerId && a.SourceId == source.Id && a.Kind == "file" && a.Basis != "user-upload", ct)) return;
        string? email = config["Sources:ContactEmail"];
        if (string.IsNullOrWhiteSpace(email))
        {
            arguments.Add(new("Cópia não consultada", "Configure Sources:ContactEmail para consultar o Unpaywall. Nenhum arquivo foi copiado.", null));
            return;
        }
        try
        {
            string baseUrl = config["Sources:Providers:UnpaywallBaseUrl"] ?? "https://api.unpaywall.org/v2";
            string endpoint = $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(source.Doi!)}?email={Uri.EscapeDataString(email)}";
            SafeHttpResponse lookup = await http.GetAsync(endpoint, ct, allowJson: true);
            using JsonDocument json = JsonDocument.Parse(lookup.Body);
            JsonElement root = json.RootElement;
            bool openAccess = root.TryGetProperty("is_oa", out JsonElement oa) && oa.ValueKind == JsonValueKind.True;
            if (!openAccess || !root.TryGetProperty("best_oa_location", out JsonElement location) || location.ValueKind != JsonValueKind.Object)
            {
                arguments.Add(new("Cópia não arquivada", "O Unpaywall não indicou uma cópia de acesso aberto; somente a referência e o link são mantidos.", null));
                return;
            }
            string? reportedLicense = location.TryGetProperty("license", out JsonElement licenseElement) ? licenseElement.GetString() : null;
            string? allowedLicense = SourcePageMetadata.NormalizeCopyLicense(reportedLicense);
            if (allowedLicense is null)
            {
                arguments.Add(new("Cópia não arquivada", $"A licença informada ({reportedLicense ?? "desconhecida"}) não autoriza cópia automática; somente o link foi mantido.", null));
                return;
            }
            string? pdfUrl = location.TryGetProperty("url_for_pdf", out JsonElement pdf) ? pdf.GetString() : null;
            if (string.IsNullOrWhiteSpace(pdfUrl))
            {
                arguments.Add(new("Cópia não arquivada", "A licença permite cópia, mas o Unpaywall não forneceu URL de PDF.", null));
                return;
            }
            SafeHttpResponse file = await http.GetAsync(pdfUrl, ct);
            if (file.StatusCode is < System.Net.HttpStatusCode.OK or >= System.Net.HttpStatusCode.MultipleChoices)
            {
                arguments.Add(new("Cópia não arquivada", $"O arquivo de acesso aberto respondeu HTTP {(int)file.StatusCode}.", file.FinalUri.ToString()));
                return;
            }
            await archives.StoreOpenAccessCopyAsync(source.OwnerId, source, file.Body, file.FinalUri.ToString(), allowedLicense, db, ct);
            arguments.Add(new("Cópia de acesso aberto arquivada", $"PDF preservado sob licença {allowedLicense}.", file.FinalUri.ToString()));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or SafeHttpException or JsonException or SourceFileException or Amazon.S3.AmazonS3Exception)
        {
            arguments.Add(new("Cópia não arquivada", $"A cópia aberta não foi preservada ({ex.GetType().Name}); o link permanece disponível.", null));
        }
    }

    private async Task AddLicensedPageCopyAsync(NoteDbContext db, Source source, SafeHttpResponse response,
        List<SourceCheckArgument> arguments, CancellationToken ct)
    {
        if (await db.SourceArchives.AnyAsync(a => a.OwnerId == source.OwnerId && a.SourceId == source.Id && a.Kind == "file", ct)) return;
        string html = response.ReadText();
        string? license = SourcePageMetadata.CreativeCommonsLicense(html);
        if (license is null)
        {
            arguments.Add(new("Cópia não arquivada", "A página não declarou uma licença Creative Commons reconhecida; somente o link e a busca por snapshot foram mantidos.", response.FinalUri.ToString()));
            return;
        }
        string text = SourcePageMetadata.ExtractVisibleText(html);
        if (string.IsNullOrWhiteSpace(text))
        {
            arguments.Add(new("Cópia não arquivada", $"A licença {license} permite a cópia, mas não foi possível extrair texto da página.", response.FinalUri.ToString()));
            return;
        }
        try
        {
            await archives.StoreLicensedTextAsync(source.OwnerId, source, text, response.FinalUri.ToString(), license, db, ct);
            arguments.Add(new("Texto de acesso aberto arquivado", $"Texto extraído preservado sob licença {license}; o HTML original não foi copiado.", response.FinalUri.ToString()));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is SourceFileException or Amazon.S3.AmazonS3Exception)
        {
            arguments.Add(new("Cópia não arquivada", $"O texto sob licença {license} não foi preservado ({ex.GetType().Name}).", response.FinalUri.ToString()));
        }
    }

    private async Task AddWaybackEvidenceAsync(NoteDbContext db, Source source, string url,
        List<SourceCheckArgument> arguments, CancellationToken ct)
    {
        try
        {
            string baseUrl = config["Sources:Providers:WaybackBaseUrl"] ?? "https://archive.org/wayback/available";
            SafeHttpResponse response = await http.GetAsync(baseUrl + (baseUrl.Contains('?') ? "&" : "?") + "url=" + Uri.EscapeDataString(url), ct, allowJson: true);
            using JsonDocument json = JsonDocument.Parse(response.Body);
            if (json.RootElement.TryGetProperty("archived_snapshots", out JsonElement snapshots) && snapshots.TryGetProperty("closest", out JsonElement closest) && closest.TryGetProperty("url", out JsonElement snapshot) && snapshot.GetString() is { } snapshotUrl)
            {
                if (IsWaybackSnapshot(snapshotUrl))
                {
                    await archives.StoreSnapshotAsync(db, source.OwnerId, source.Id, url, snapshotUrl, ct);
                    arguments.Add(new("Snapshot disponível", "A página tem uma captura no Internet Archive.", snapshotUrl));
                }
                else arguments.Add(new("Snapshot ignorado", "O link retornado não corresponde a uma captura válida do Internet Archive.", null));
            }
            else if (config.GetValue<bool>("Sources:Wayback:SaveEnabled"))
            {
                string saveBase = config["Sources:Providers:WaybackSaveBaseUrl"] ?? "https://web.archive.org/save/";
                SafeHttpResponse saved = await http.GetAsync(saveBase + Uri.EscapeDataString(url), ct);
                if ((int)saved.StatusCode < 400 && IsWaybackSnapshot(saved.FinalUri.ToString()))
                {
                    await archives.StoreSnapshotAsync(db, source.OwnerId, source.Id, url, saved.FinalUri.ToString(), ct);
                    arguments.Add(new("Snapshot disponível", "O Internet Archive retornou uma captura após a solicitação.", saved.FinalUri.ToString()));
                }
                else if ((int)saved.StatusCode < 400) arguments.Add(new("Snapshot solicitado", "O Internet Archive recebeu o pedido; a captura ainda não tem um link verificável.", null));
                else arguments.Add(new("Snapshot não solicitado", $"O Internet Archive respondeu HTTP {(int)saved.StatusCode}.", saved.FinalUri.ToString()));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or SafeHttpException or JsonException)
        { arguments.Add(new("Snapshot indisponível", $"Não foi possível consultar o Internet Archive ({ex.GetType().Name}).", null)); }
    }

    private static bool IsWaybackSnapshot(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/web/", StringComparison.Ordinal);
    private static string? First(JsonElement root, string key) => root.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0 ? v[0].GetString() : null;
    private static string? GetString(JsonElement root, string key) => root.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
