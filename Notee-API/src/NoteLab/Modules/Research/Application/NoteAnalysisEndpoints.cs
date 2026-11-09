using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoteLab.Domain;

namespace NoteLab.Application;

public record NoteSuggestionResponse(Guid Id, string Kind, string Value, string Reason, string Status, long NoteVersion, string Provider, DateTimeOffset CreatedAt);
public record NoteSettingsResponse(string AutoResearch, bool EffectiveAutoResearch, long Version);
public record UpdateNoteSettingsRequest(string AutoResearch);

/// <summary>
/// Análise assistida (M10): sugere tags, categoria e dúvidas a partir do texto da nota.
/// O analisador atual é local e determinístico ("fake"): nada é enviado para fora.
/// Cada sugestão é aceita individualmente e revalidada contra a versão da nota.
/// </summary>
public static partial class NoteAnalysisEndpoints
{
    private const int MaxTagSuggestions = 3;
    private const int MaxDoubtSuggestions = 3;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "sobre", "entre", "quando", "porque", "também", "assim", "ainda", "outro", "outra", "outros", "outras", "sendo",
        "pelos", "pelas", "desta", "deste", "nesta", "neste", "dessa", "desse", "essas", "esses", "estas", "estes",
        "mesmo", "mesma", "todas", "todos", "muito", "muita", "maior", "menor", "forma", "parte", "tempo", "coisa",
        "porém", "contudo", "seria", "podem", "pode", "fazer", "feito", "sempre", "nunca", "antes", "depois",
    };

    public static void MapNoteAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization().WithTags("Analysis");

        group.MapPost("/notes/{noteId:guid}/analysis-runs", async (Guid noteId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            Note? note = await db.Notes.AsNoTracking().Include(n => n.Tags)
                .SingleOrDefaultAsync(n => n.Id == noteId && n.OwnerId == owner && n.Status != NoteStatus.Trashed, ct);
            if (note is null)
            {
                return Results.NotFound();
            }

            List<NoteSuggestion> created = await AnalyzeAsync(db, note, ct);
            db.NoteSuggestions.AddRange(created);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ListAsync(db, owner, noteId, ct));
        });

        group.MapGet("/notes/{noteId:guid}/suggestions", async (Guid noteId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            return await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct)
                ? Results.Ok(await ListAsync(db, owner, noteId, ct))
                : Results.NotFound();
        });

        group.MapPost("/suggestions/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            NoteSuggestion? s = await db.NoteSuggestions.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == Owner(user), ct);
            if (s is null)
            {
                return Results.NotFound();
            }

            if (s.Status != NoteSuggestionStatus.Pending)
            {
                return Conflict("A sugestão já foi decidida.");
            }

            s.Decide(NoteSuggestionStatus.Dismissed);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/suggestions/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, NoteDbContext db, NoteRepository notes, ResearchService research, CancellationToken ct) =>
        {
            string owner = Owner(user);
            NoteSuggestion? s = await db.NoteSuggestions.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
            if (s is null)
            {
                return Results.NotFound();
            }

            if (s.Status != NoteSuggestionStatus.Pending)
            {
                return Conflict("A sugestão já foi decidida.");
            }

            Note? note = await db.Notes.Include(n => n.Tags).SingleOrDefaultAsync(n => n.Id == s.NoteId && n.OwnerId == owner, ct);
            if (note is null || note.Status == NoteStatus.Trashed || note.Version != s.NoteVersion)
            {
                s.Decide(NoteSuggestionStatus.Stale);
                await db.SaveChangesAsync(ct);
                return Conflict("Sugestão obsoleta: a nota mudou depois da análise. Analise novamente.");
            }

            switch (s.Kind)
            {
                case NoteSuggestionKind.Tag:
                {
                    Tag tag = await db.Tags.FirstOrDefaultAsync(t => t.OwnerId == owner && t.Name.ToLower() == s.Value.ToLower(), ct)
                              ?? Tag.Create(owner, s.Value);
                    if (db.Entry(tag).State == EntityState.Detached)
                    {
                        db.Tags.Add(tag);
                        await db.SaveChangesAsync(ct);
                    }

                    // Mesmo caminho do PUT: nova versão + revisão da nota.
                    await notes.UpdateNoteAsync(owner, note.Version, note.Id, note.Title.Value, note.Content ?? "", note.CategoryId,
                        note.Tags.Select(t => t.Id).Append(tag.Id).Distinct().ToList(), ct);
                    break;
                }
                case NoteSuggestionKind.Category:
                {
                    Category? category = await db.Categories.FirstOrDefaultAsync(c => c.OwnerId == owner && c.Name.ToLower() == s.Value.ToLower(), ct);
                    if (category is null)
                    {
                        s.Decide(NoteSuggestionStatus.Stale);
                        await db.SaveChangesAsync(ct);
                        return Conflict("A categoria sugerida não existe mais.");
                    }

                    await notes.UpdateNoteAsync(owner, note.Version, note.Id, note.Title.Value, note.Content ?? "", category.Id, null, ct);
                    break;
                }
                case NoteSuggestionKind.Doubt:
                {
                    (string blockId, int start)? anchor = FindSentence(note.Content, s.Value);
                    if (anchor is null)
                    {
                        s.Decide(NoteSuggestionStatus.Stale);
                        await db.SaveChangesAsync(ct);
                        return Conflict("O trecho não foi encontrado na nota.");
                    }

                    var result = await research.CreateSelectionDoubtAsync(owner,
                        new CreateDoubtRequest(note.Id, s.Value, anchor.Value.blockId, s.Value, anchor.Value.start, anchor.Value.start + s.Value.Length), ct);
                    if (!result.IsSuccess)
                    {
                        return BoardEndpoints.ToProblem(result);
                    }

                    break;
                }
            }

            s.Decide(NoteSuggestionStatus.Accepted);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(s));
        });

        // ---- Configuração por nota (precedência nota > conta) ----
        group.MapGet("/notes/{noteId:guid}/settings", async (Guid noteId, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct))
            {
                return Results.NotFound();
            }

            NoteSettings settings = await db.NoteSettings.AsNoTracking().SingleOrDefaultAsync(s => s.NoteId == noteId, ct) ?? new NoteSettings(noteId, owner);
            return Results.Ok(await SettingsResponseAsync(db, owner, settings, ct));
        });

        group.MapPatch("/notes/{noteId:guid}/settings", async (Guid noteId, UpdateNoteSettingsRequest request, ClaimsPrincipal user, NoteDbContext db, CancellationToken ct) =>
        {
            string owner = Owner(user);
            if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.OwnerId == owner, ct))
            {
                return Results.NotFound();
            }

            if (!Enum.TryParse(request.AutoResearch, ignoreCase: true, out AutoResearchOverride value) || int.TryParse(request.AutoResearch, out _))
            {
                return Results.Problem(statusCode: 400, title: "Erro de validação", detail: "Use inherit, on ou off.");
            }

            NoteSettings? settings = await db.NoteSettings.SingleOrDefaultAsync(s => s.NoteId == noteId, ct);
            if (settings is null)
            {
                settings = new NoteSettings(noteId, owner);
                db.NoteSettings.Add(settings);
            }

            // Mudar a configuração não enfileira dúvidas antigas (sem backfill inesperado).
            settings.SetAutoResearch(value);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await SettingsResponseAsync(db, owner, settings, ct));
        });
    }

    // ---- Analisador local (determinístico) ----

    private static async Task<List<NoteSuggestion>> AnalyzeAsync(NoteDbContext db, Note note, CancellationToken ct)
    {
        string text = NoteText.Extract(note.Content, 20_000);
        string all = note.Title.Value + "\n" + text;

        // Pendentes de versões anteriores ficam obsoletas: serão recalculadas para a versão atual.
        List<NoteSuggestion> outdated = await db.NoteSuggestions
            .Where(s => s.NoteId == note.Id && s.Status == NoteSuggestionStatus.Pending && s.NoteVersion != note.Version)
            .ToListAsync(ct);
        outdated.ForEach(s => s.Decide(NoteSuggestionStatus.Stale));

        // Não repetir: pendentes da versão atual e o que a pessoa já descartou.
        var existingPending = (await db.NoteSuggestions.AsNoTracking()
                .Where(s => s.NoteId == note.Id
                            && ((s.Status == NoteSuggestionStatus.Pending && s.NoteVersion == note.Version) || s.Status == NoteSuggestionStatus.Dismissed))
                .Select(s => new { s.Kind, s.Value }).ToListAsync(ct))
            .Select(s => (s.Kind, s.Value.ToLowerInvariant())).ToHashSet();
        HashSet<string> currentTags = note.Tags.Select(t => t.Name.ToLowerInvariant()).ToHashSet();
        var result = new List<NoteSuggestion>();

        // Tags: termos mais frequentes (ou tags já existentes da conta citadas no texto).
        List<string> ownerTags = await db.Tags.AsNoTracking().Where(t => t.OwnerId == note.OwnerId).Select(t => t.Name).ToListAsync(ct);
        IEnumerable<string> mentioned = ownerTags.Where(t => !currentTags.Contains(t.ToLowerInvariant())
                                                             && Regex.IsMatch(all, $@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase));
        IEnumerable<string> frequent = Words().Matches(all.ToLowerInvariant()).Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .GroupBy(w => w).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key);
        foreach (string tag in mentioned.Concat(frequent).Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(t => !currentTags.Contains(t.ToLowerInvariant()) && t.Length <= 60 && !existingPending.Contains((NoteSuggestionKind.Tag, t.ToLowerInvariant())))
                     .Take(MaxTagSuggestions))
        {
            result.Add(new NoteSuggestion(note, NoteSuggestionKind.Tag, tag, $"O termo “{tag}” aparece com destaque na nota.", "fake"));
        }

        // Categoria: categoria existente da conta cujo nome aparece no texto (só se a nota ainda não tem).
        if (note.CategoryId is null)
        {
            List<string> categories = await db.Categories.AsNoTracking().Where(c => c.OwnerId == note.OwnerId).Select(c => c.Name).ToListAsync(ct);
            string? match = categories.FirstOrDefault(c => Regex.IsMatch(all, $@"\b{Regex.Escape(c)}\b", RegexOptions.IgnoreCase)
                                                           && !existingPending.Contains((NoteSuggestionKind.Category, c.ToLowerInvariant())));
            if (match is not null)
            {
                result.Add(new NoteSuggestion(note, NoteSuggestionKind.Category, match, $"A nota menciona “{match}”, uma das suas categorias.", "fake"));
            }
        }

        // Dúvidas: frases interrogativas que ainda não viraram dúvida.
        HashSet<string> existingDoubts = (await db.Doubts.AsNoTracking().Where(d => d.NoteId == note.Id).Select(d => d.Question).ToListAsync(ct))
            .Select(q => q.ToLowerInvariant()).ToHashSet();
        foreach (string question in Questions().Matches(text).Select(m => m.Value.Trim())
                     .Where(q => q.Length is >= 8 and <= NoteSuggestion.ValueMaxLength)
                     .Where(q => !existingDoubts.Contains(q.ToLowerInvariant()) && !existingPending.Contains((NoteSuggestionKind.Doubt, q.ToLowerInvariant())))
                     .Distinct().Take(MaxDoubtSuggestions))
        {
            result.Add(new NoteSuggestion(note, NoteSuggestionKind.Doubt, question, "Pergunta escrita na nota que ainda não foi registrada como dúvida.", "fake"));
        }

        return result;
    }

    /// <summary>Localiza a frase em um bloco do editor para criar âncora real (sem inventar posição).</summary>
    private static (string blockId, int start)? FindSentence(string? content, string sentence)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement block in doc.RootElement.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out JsonElement t) && t.GetString() is { } text
                        && block.TryGetProperty("id", out JsonElement idEl) && idEl.GetString() is { } blockId)
                    {
                        int index = text.IndexOf(sentence, StringComparison.Ordinal);
                        if (index >= 0)
                        {
                            return (blockId, index);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // conteúdo antigo em texto puro: sem bloco identificável
        }

        return null;
    }

    private static async Task<List<NoteSuggestionResponse>> ListAsync(NoteDbContext db, string owner, Guid noteId, CancellationToken ct) =>
        (await db.NoteSuggestions.AsNoTracking()
            .Where(s => s.NoteId == noteId && s.OwnerId == owner && s.Status == NoteSuggestionStatus.Pending)
            .OrderBy(s => s.Kind).ThenBy(s => s.CreatedAt)
            .ToListAsync(ct))
        .Select(ToResponse).ToList();

    private static NoteSuggestionResponse ToResponse(NoteSuggestion s) =>
        new(s.Id, s.Kind.ToString().ToLowerInvariant(), s.Value, s.Reason, s.Status.ToString(), s.NoteVersion, s.Provider, s.CreatedAt);

    private static async Task<NoteSettingsResponse> SettingsResponseAsync(NoteDbContext db, string owner, NoteSettings settings, CancellationToken ct)
    {
        UserPreferences prefs = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? UserPreferences.Default(owner);
        return new NoteSettingsResponse(settings.AutoResearch.ToString().ToLowerInvariant(), NoteSettings.ShouldAutoResearch(prefs, settings.AutoResearch), settings.Version);
    }

    private static IResult Conflict(string detail) => Results.Problem(statusCode: 409, title: "Conflito", detail: detail);

    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();

    [GeneratedRegex(@"[\p{L}]{5,}")]
    private static partial Regex Words();

    [GeneratedRegex(@"[^.!?\n]*\?")]
    private static partial Regex Questions();
}
