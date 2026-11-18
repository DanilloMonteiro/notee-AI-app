using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NoteLab.Domain;

namespace NoteLab.Application;

public enum ComposeMode
{
    Generate,
    Rewrite
}

/// <summary>
/// Fonte sugerida pela IA, ainda não conferida. <see cref="Supports"/>: trechos EXATOS do texto gerado que a fonte apoia
/// (só ficam os que existem no texto; viram citações ancoradas no cliente).
/// </summary>
public sealed record ComposeSourceSuggestion(string Identifier, string Reason, IReadOnlyList<string>? Supports = null);

/// <summary>
/// Origem informada pela IA SEM identificador conferível (ex.: "IBGE, Censo 2022"), ou referência cujo DOI/ISBN/URL
/// não foi encontrado. Exibida como "Origem informada pela IA — não conferida"; nunca recebe link.
/// </summary>
public sealed record ComposeOrigin(string Label, IReadOnlyList<string> Supports, bool FromUnresolvedIdentifier = false);

/// <summary>Gráfico novo proposto pela IA: bloco no formato de NoteContent (sem fontes) + identificador da fonte dos dados.</summary>
public sealed record ComposeChartDraft(JsonObject Chart, string SourceIdentifier, string SourceReason);

/// <summary>
/// Resposta interpretada. Blocos de gráfico usam <c>type = "chart"</c> (gráfico da nota base, por índice)
/// ou <c>type = "newchart"</c> (índice em <see cref="NewCharts"/>).
/// </summary>
public sealed record ComposeDraft(
    string? Title,
    IReadOnlyList<OrganizedBlock> Blocks,
    IReadOnlyList<ComposeChartDraft> NewCharts,
    TaxonomyPreview Taxonomy,
    IReadOnlyList<ComposeSourceSuggestion> Sources,
    int DiscardedCharts,
    IReadOnlyList<ComposeOrigin>? Origins = null);

/// <summary>Fonte sugerida que foi encontrada pela resolução de DOI/ISBN/URL, com os trechos que apoia.</summary>
public sealed record ComposeResolvedSource(string Identifier, string Reason, ResolvedSource Resolved, IReadOnlyList<string>? Supports = null);

/// <summary>Gráfico da prévia: <c>base</c> (copiado da nota base) ou <c>generated</c> (com fonte conferida em <see cref="SourceIndex"/>).</summary>
public sealed record ComposeChartOut(JsonObject Chart, string Origin, int? SourceIndex);

public sealed record ComposeResult(
    string? Title,
    IReadOnlyList<OrganizedBlock> Blocks,
    IReadOnlyList<ComposeChartOut> Charts,
    TaxonomyPreview Taxonomy,
    IReadOnlyList<ComposeResolvedSource> Sources,
    int DiscardedSources,
    int DiscardedCharts,
    IReadOnlyList<ComposeOrigin>? Origins = null);

/// <summary>Texto da nota base enviado à IA; gráficos viram marcadores [[grafico:N]] e voltam intactos.</summary>
public sealed record ComposeBaseText(string Text, IReadOnlyList<JsonObject> Charts);

/// <summary>
/// Criação de nota assistida por IA (ADR-044): prompt fixo, leitura da resposta e simulação local.
/// Nada aqui grava dados; o resultado é uma prévia que a pessoa aceita no cliente.
/// </summary>
public static partial class NoteComposer
{
    public const string Marker = "NOTE-COMPOSE";
    public const int MaxInstructionsChars = 4_000;
    public const int MaxSources = 5;
    public const int MaxOrigins = 5;
    public const int MaxSupportsPerSource = 5;
    public const int MaxSupportLength = 500;
    public const int MaxOriginLabelLength = 200;
    public const int MaxNewCharts = 3;
    public const int MaxOutputTokens = 8_000;

    public const string SystemPrompt =
        Marker + "\n" +
        "Você ajuda a criar uma nota de estudo em português. Responda SOMENTE com JSON no formato " +
        "{\"title\": string|null, " +
        "\"blocks\": [{\"type\": \"heading\"|\"paragraph\"|\"list\"|\"quote\"|\"code\"|\"table\", \"level\": 1|2|3 (só heading), \"text\": string} " +
        "| {\"type\": \"chart\", \"ref\": N} | {\"type\": \"newchart\", \"ref\": N}], " +
        "\"charts\": [{\"chartType\": \"line\"|\"bar\", \"title\": string, \"labels\": [string], \"series\": [{\"name\": string, \"values\": [number|null]}], " +
        "\"sourceIdentifier\": \"DOI, ISBN ou URL\", \"sourceReason\": string}], " +
        "\"category\": {\"name\": string, \"reason\": string}|null, \"tags\": [{\"name\": string, \"reason\": string}], " +
        "\"sources\": [{\"identifier\": \"DOI, ISBN ou URL\", \"reason\": string, \"supports\": [string]}], " +
        "\"origins\": [{\"label\": string, \"supports\": [string]}]}. " +
        "Regras: no modo \"generate\", escreva a nota a partir do título e da descrição; no modo \"rewrite\", modifique a nota base " +
        "conforme a instrução. Itens de lista ficam um por linha em \"text\", sem marcadores; sem markdown (#, **). " +
        "Cada marcador [[grafico:N]] da nota base é um gráfico existente: mantenha-o como {\"type\":\"chart\",\"ref\":N} e nunca altere seus números. " +
        "Gráficos novos vão em \"charts\" e entram no texto como {\"type\":\"newchart\",\"ref\":índice}; no máximo 3. " +
        "Só crie gráfico com dados numéricos tirados de uma fonte real e identificável (DOI, ISBN ou URL) em \"sourceIdentifier\"; " +
        "sem fonte, NÃO crie o gráfico. Em \"sources\", só DOI, ISBN ou URL que você tenha certeza de que existem; " +
        "na dúvida, deixe a lista vazia. Não invente dados, números, citações nem fontes. " +
        "Em \"supports\" de cada fonte, copie EXATAMENTE (mesmas palavras e pontuação) de 1 a 5 trechos curtos do texto da nota " +
        "que vieram dessa fonte. Em \"origins\", liste de onde vieram informações que não têm DOI, ISBN ou URL " +
        "(ex.: \"IBGE, Censo 2022\", \"Relatório anual do Banco Central\"), também com \"supports\"; " +
        "se a informação vem de conhecimento geral sem origem identificável, não a liste. " +
        "Categoria e até cinco tags: prefira nomes existentes quando combinarem.";

    private static readonly string[] TextTypes = ["heading", "paragraph", "list", "quote", "code", "table"];

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string UserPrompt(ComposeMode mode, string title, string? instructions, string? baseTitle, ComposeBaseText? baseText,
        IEnumerable<Category> categories, IEnumerable<Tag> tags) =>
        JsonSerializer.Serialize(new
        {
            mode = mode == ComposeMode.Generate ? "generate" : "rewrite",
            title,
            instructions = instructions ?? "",
            baseNote = baseText is null ? null : new { title = baseTitle, text = baseText.Text },
            categories = categories.Select(c => c.Name),
            tags = tags.Select(t => t.Name),
        }, Json);

    /// <summary>Estimativa grosseira (≈ 4 caracteres por token), igual à do organizar com IA.</summary>
    public static int EstimateTokens(string userPrompt) => (int)Math.Ceiling((SystemPrompt.Length + userPrompt.Length) / 4.0);

    /// <summary>Converte o conteúdo salvo (blocos JSON ou texto puro) em texto com marcação leve, como o cliente faz no organizar.</summary>
    public static ComposeBaseText BaseText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new("", []);
        }

        JsonArray? blocks = null;
        if (content.TrimStart().StartsWith('['))
        {
            try
            {
                blocks = JsonNode.Parse(content) as JsonArray;
            }
            catch (JsonException)
            {
                // texto puro que começa com "["
            }
        }

        if (blocks is null)
        {
            return new(content.Trim(), []);
        }

        var charts = new List<JsonObject>();
        var parts = new List<string>();
        foreach (JsonNode? node in blocks)
        {
            if (node is not JsonObject block)
            {
                continue;
            }

            string type = block["type"]?.GetValue<string>() ?? "paragraph";
            if (type == NoteContent.ChartType)
            {
                charts.Add((JsonObject)block.DeepClone());
                parts.Add($"[[grafico:{charts.Count - 1}]]");
                continue;
            }

            string text = block["text"] is JsonValue v && v.TryGetValue(out string? s) ? s ?? "" : "";
            if (text.Trim().Length == 0)
            {
                continue;
            }

            parts.Add(type switch
            {
                "heading" => $"{new string('#', Math.Clamp(block["level"] is JsonValue l && l.TryGetValue(out int level) ? level : 2, 1, 3))} {text.Replace('\n', ' ')}",
                "list" => string.Join('\n', text.Split('\n').Select(line => $"- {line}")),
                "quote" => string.Join('\n', text.Split('\n').Select(line => $"> {line}")),
                "code" => $"```\n{text}\n```",
                _ => text,
            });
        }

        return new(string.Join("\n\n", parts), charts);
    }

    /// <summary>
    /// Interpreta a resposta. Taxonomia reaproveita <see cref="TaxonomyAi.Parse"/>. Gráficos novos inválidos ou sem fonte
    /// são descartados junto com o bloco que os referencia; referências a gráficos base inexistentes também saem.
    /// </summary>
    public static ComposeDraft Parse(string aiText, int baseChartCount, IEnumerable<Category> categories, IEnumerable<Tag> tags)
    {
        string body = aiText.Trim();
        int open = body.IndexOf('{');
        int close = body.LastIndexOf('}');
        if (open < 0 || close <= open)
        {
            throw new OrganizeRejectedException("A IA não devolveu JSON válido; nada foi criado.");
        }

        string json = body[open..(close + 1)];
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject ?? throw new OrganizeRejectedException("A IA não devolveu JSON válido; nada foi criado.");
        }
        catch (JsonException)
        {
            throw new OrganizeRejectedException("A IA não devolveu JSON válido; nada foi criado.");
        }

        // Gráficos novos: valida cada um; o índice original é remapeado para a posição entre os aceitos.
        var newCharts = new List<ComposeChartDraft>();
        var chartMap = new Dictionary<int, int>();
        int discarded = 0;
        if (root["charts"] is JsonArray chartRows)
        {
            for (int i = 0; i < chartRows.Count; i++)
            {
                if (newCharts.Count < MaxNewCharts && ParseChart(chartRows[i]) is { } chart)
                {
                    chartMap[i] = newCharts.Count;
                    newCharts.Add(chart);
                }
                else
                {
                    discarded++;
                }
            }
        }

        var blocks = new List<OrganizedBlock>();
        var usedBase = new HashSet<int>();
        var usedNew = new HashSet<int>();
        if (root["blocks"] is JsonArray rows)
        {
            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject block)
                {
                    continue;
                }

                string type = (Str(block, "type") ?? "").Trim().ToLowerInvariant();
                int? reference = block["ref"] is JsonValue r && r.TryGetValue(out int n) ? n : null;
                if (type == "chart")
                {
                    if (reference is { } baseRef && baseRef >= 0 && baseRef < baseChartCount && usedBase.Add(baseRef))
                    {
                        blocks.Add(new OrganizedBlock("chart", Ref: baseRef));
                    }

                    continue;
                }

                if (type == "newchart")
                {
                    if (reference is { } newRef && chartMap.TryGetValue(newRef, out int mapped) && usedNew.Add(mapped))
                    {
                        blocks.Add(new OrganizedBlock("newchart", Ref: mapped));
                    }

                    continue;
                }

                string text = Str(block, "text") ?? "";
                if (!TextTypes.Contains(type) || text.Trim().Length == 0)
                {
                    continue;
                }

                int? level = type == "heading" ? Math.Clamp(block["level"] is JsonValue lv && lv.TryGetValue(out int l) ? l : 2, 1, 3) : null;
                blocks.Add(new OrganizedBlock(type, text, level));
            }
        }

        // Gráfico novo que a IA não posicionou no texto vai para o fim (continua exigindo fonte).
        for (int i = 0; i < newCharts.Count; i++)
        {
            if (!usedNew.Contains(i))
            {
                blocks.Add(new OrganizedBlock("newchart", Ref: i));
            }
        }

        if (!blocks.Any(b => b.Type is not ("chart" or "newchart")))
        {
            throw new OrganizeRejectedException("A IA não devolveu texto para a nota; nada foi criado.");
        }

        TaxonomyPreview taxonomy;
        try
        {
            taxonomy = TaxonomyAi.Parse(json, categories, tags);
        }
        catch (JsonException)
        {
            taxonomy = new TaxonomyPreview(null, []);
        }

        // Texto gerado: os trechos de "supports" só valem se existirem nele (nada de citação para texto que não está na nota).
        string noteText = string.Join("\n", blocks.Where(b => b.Text is not null).Select(b => b.Text));

        var sources = new List<ComposeSourceSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["sources"] is JsonArray sourceRows)
        {
            foreach (JsonNode? row in sourceRows)
            {
                string identifier = (row is JsonObject o ? Str(o, "identifier") : null)?.Trim() ?? "";
                if (identifier.Length is < 1 or > NoteContent.MaxUrlLength || !seen.Add(identifier))
                {
                    continue;
                }

                var source = (JsonObject)row!;
                sources.Add(new ComposeSourceSuggestion(identifier, Clip(Str(source, "reason") ?? "Sugestão da IA", 200),
                    ParseSupports(source, noteText)));
                if (sources.Count == MaxSources)
                {
                    break;
                }
            }
        }

        var origins = new List<ComposeOrigin>();
        var seenOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["origins"] is JsonArray originRows)
        {
            foreach (JsonNode? row in originRows)
            {
                if (row is not JsonObject origin)
                {
                    continue;
                }

                string label = Clip(Str(origin, "label")?.Trim() ?? "", MaxOriginLabelLength);
                if (label.Length == 0 || !seenOrigins.Add(label))
                {
                    continue;
                }

                origins.Add(new ComposeOrigin(label, ParseSupports(origin, noteText)));
                if (origins.Count == MaxOrigins)
                {
                    break;
                }
            }
        }

        string? title = Str(root, "title")?.Trim();
        return new ComposeDraft(string.IsNullOrEmpty(title) ? null : Clip(title, 100), blocks, newCharts, taxonomy, sources, discarded, origins);
    }

    /// <summary>Trechos de "supports" que existem EXATAMENTE no texto da nota (sem duplicatas, até o limite).</summary>
    private static List<string> ParseSupports(JsonObject row, string noteText)
    {
        var supports = new List<string>();
        if (row["supports"] is not JsonArray items)
        {
            return supports;
        }

        foreach (JsonNode? item in items)
        {
            string quote = item is JsonValue v && v.TryGetValue(out string? s) ? s?.Trim() ?? "" : "";
            if (quote.Length is < 3 or > MaxSupportLength || supports.Contains(quote) || !noteText.Contains(quote, StringComparison.Ordinal))
            {
                continue;
            }

            supports.Add(quote);
            if (supports.Count == MaxSupportsPerSource)
            {
                break;
            }
        }

        return supports;
    }

    /// <summary>Monta o bloco no formato de NoteContent e valida com a mesma regra do salvamento. Sem fonte → null.</summary>
    private static ComposeChartDraft? ParseChart(JsonNode? node)
    {
        if (node is not JsonObject row)
        {
            return null;
        }

        string? sourceIdentifier = Str(row, "sourceIdentifier")?.Trim();
        if (string.IsNullOrEmpty(sourceIdentifier) || sourceIdentifier.Length > NoteContent.MaxUrlLength)
        {
            return null;
        }

        var chart = new JsonObject
        {
            ["type"] = NoteContent.ChartType,
            ["schemaVersion"] = NoteContent.ChartSchemaVersion,
            ["chartType"] = Str(row, "chartType")?.Trim().ToLowerInvariant(),
            ["title"] = Clip(Str(row, "title")?.Trim() ?? "", NoteContent.MaxTitleLength),
            ["labels"] = row["labels"]?.DeepClone(),
            ["series"] = row["series"]?.DeepClone(),
            ["sources"] = new JsonArray(),
        };

        try
        {
            NoteContent.Validate(new JsonArray(chart.DeepClone()).ToJsonString());
        }
        catch (Exception)
        {
            return null; // qualquer violação do schema de gráfico = gráfico descartado
        }

        return new ComposeChartDraft(chart, sourceIdentifier, Clip(Str(row, "sourceReason") ?? "Dados atribuídos pela IA", 200));
    }

    /// <summary>
    /// Aplica a conferência das fontes: só ficam fontes resolvidas; gráfico novo só fica se a fonte dos dados resolveu
    /// (ela entra também na lista de fontes da nota). Gráficos da base voltam intactos. Refs passam a apontar para <see cref="ComposeResult.Charts"/>.
    /// </summary>
    public static ComposeResult Assemble(ComposeDraft draft, IReadOnlyList<JsonObject> baseCharts,
        IReadOnlyDictionary<string, ResolvedSource> resolved)
    {
        var sources = new List<ComposeResolvedSource>();
        var origins = new List<ComposeOrigin>(draft.Origins ?? []);
        foreach (ComposeSourceSuggestion s in draft.Sources)
        {
            if (resolved.TryGetValue(s.Identifier, out ResolvedSource? r))
            {
                sources.Add(new ComposeResolvedSource(s.Identifier, s.Reason, r, s.Supports ?? []));
            }
            else
            {
                // DOI/ISBN/URL não encontrado: não vira fonte conferida, mas a origem declarada continua visível como "não conferida".
                origins.Add(new ComposeOrigin(Clip(s.Identifier, MaxOriginLabelLength), s.Supports ?? [], FromUnresolvedIdentifier: true));
            }
        }

        int discardedSources = draft.Sources.Count - sources.Count;
        int discardedCharts = draft.DiscardedCharts;
        var charts = new List<ComposeChartOut>();
        var blocks = new List<OrganizedBlock>();
        foreach (OrganizedBlock block in draft.Blocks)
        {
            if (block.Type == "chart")
            {
                charts.Add(new ComposeChartOut((JsonObject)baseCharts[block.Ref!.Value].DeepClone(), "base", null));
                blocks.Add(new OrganizedBlock("chart", Ref: charts.Count - 1));
                continue;
            }

            if (block.Type == "newchart")
            {
                ComposeChartDraft draftChart = draft.NewCharts[block.Ref!.Value];
                if (!resolved.TryGetValue(draftChart.SourceIdentifier, out ResolvedSource? source))
                {
                    discardedCharts++;
                    continue;
                }

                int sourceIndex = sources.FindIndex(x => string.Equals(x.Identifier, draftChart.SourceIdentifier, StringComparison.OrdinalIgnoreCase));
                if (sourceIndex < 0)
                {
                    sources.Add(new ComposeResolvedSource(draftChart.SourceIdentifier, draftChart.SourceReason, source));
                    sourceIndex = sources.Count - 1;
                }

                var chart = (JsonObject)draftChart.Chart.DeepClone();
                chart["sources"] = new JsonArray(ChartSource(source));
                charts.Add(new ComposeChartOut(chart, "generated", sourceIndex));
                blocks.Add(new OrganizedBlock("chart", Ref: charts.Count - 1));
                continue;
            }

            blocks.Add(block);
        }

        return new ComposeResult(draft.Title, blocks, charts, draft.Taxonomy, sources, discardedSources, discardedCharts, origins);
    }

    private static JsonObject ChartSource(ResolvedSource source)
    {
        string label = source.Isbn is { } isbn ? $"{source.Title} (ISBN {isbn})" : source.Title;
        string? url = source.Url ?? (source.Doi is { } doi ? $"https://doi.org/{doi}" : null);
        var node = new JsonObject { ["label"] = Clip(label, NoteContent.MaxSourceLabelLength) };
        if (url is not null && NoteContent.IsHttpUrl(url) && url.Length <= NoteContent.MaxUrlLength)
        {
            node["url"] = url;
        }

        return node;
    }

    // ---- Provedor simulado (sem custo, determinístico, sem fontes e sem gráficos novos) ----

    /// <summary>Resposta do Fake para o prompt de <see cref="UserPrompt"/>.</summary>
    public static string FakeCompletion(string userPrompt)
    {
        using JsonDocument doc = JsonDocument.Parse(userPrompt);
        JsonElement root = doc.RootElement;
        string title = root.TryGetProperty("title", out JsonElement t) ? t.GetString() ?? "" : "";
        string instructions = root.TryGetProperty("instructions", out JsonElement i) ? i.GetString() ?? "" : "";
        string? baseText = root.TryGetProperty("baseNote", out JsonElement b) && b.ValueKind == JsonValueKind.Object
            ? b.GetProperty("text").GetString() : null;
        string[] categoryNames = Names(root, "categories");
        string[] tagNames = Names(root, "tags");

        var blocks = new List<object>();
        if (!string.IsNullOrWhiteSpace(baseText))
        {
            // Mantém a estrutura (e os gráficos) da base e registra o pedido de mudança.
            OrganizedNote organized = NoteAiOrganizer.Parse(NoteAiOrganizer.FakeOrganize(baseText));
            blocks.AddRange(organized.Blocks.Select(x => x.Type == "chart"
                ? (object)new { type = "chart", @ref = x.Ref }
                : new { type = x.Type, text = x.Text, level = x.Level }));
            if (instructions.Trim().Length > 0)
            {
                blocks.Add(new { type = "heading", text = "Ajustes pedidos", level = (int?)2 });
                blocks.Add(new { type = "paragraph", text = instructions.Trim(), level = (int?)null });
            }
        }
        else
        {
            blocks.Add(new { type = "paragraph", text = instructions.Trim().Length > 0 ? instructions.Trim() : title, level = (int?)null });
            string[] points = Clauses().Matches(instructions).Select(m => m.Value.Trim()).Where(p => p.Length > 3).Take(5).ToArray();
            if (points.Length > 0)
            {
                blocks.Add(new { type = "heading", text = "Pontos para desenvolver", level = (int?)2 });
                blocks.Add(new { type = "list", text = string.Join('\n', points), level = (int?)null });
            }
        }

        var note = new Note(new NoteTitle(Clip(title.Trim().Length > 0 ? title : "Nota", 100)), "fake", null);
        note.SetPlainText($"{instructions}\n{baseText}");
        TaxonomyPreview taxonomy = TaxonomyAi.Fake(note,
            categoryNames.Select(n => Category.Create("fake", n)), tagNames.Select(n => Tag.Create("fake", n)));

        return JsonSerializer.Serialize(new
        {
            title = (string?)null,
            blocks,
            charts = Array.Empty<object>(),
            category = taxonomy.Category is null ? null : new { name = taxonomy.Category.Name, reason = taxonomy.Category.Reason },
            tags = taxonomy.Tags.Select(x => new { name = x.Name, reason = x.Reason }),
            sources = Array.Empty<object>(),
        }, Json);
    }

    private static string[] Names(JsonElement root, string property) =>
        root.TryGetProperty(property, out JsonElement list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)
                .Where(n => n.Trim().Length is > 0 and <= Category.NameMaxLength).ToArray()
            : [];

    private static string? Str(JsonObject obj, string property) =>
        obj[property] is JsonValue value && value.TryGetValue(out string? s) ? s : null;

    private static string Clip(string value, int max) => value.Length > max ? value[..max] : value;

    [GeneratedRegex(@"[^.;!?\n]+")]
    private static partial Regex Clauses();
}

/// <summary>Link de outra nota aceito na criação: nota própria, publicação pública ou link secreto.</summary>
public abstract record NoteLink
{
    public sealed record Own(Guid NoteId) : NoteLink;
    public sealed record Public(string Handle, string Slug) : NoteLink;
    public sealed record Shared(string Token) : NoteLink;
}

public static partial class NoteLinkParser
{
    /// <summary>Aceita URL absoluta ou caminho: /note/{id}, /p/{handle}/{slug}, /s/{token}. Null se não reconhecer.</summary>
    public static NoteLink? Parse(string? input)
    {
        string value = (input ?? "").Trim();
        if (value.Length is 0 or > 2048)
        {
            return null;
        }

        string path;
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            path = uri.AbsolutePath;
        }
        else if (value.StartsWith('/'))
        {
            path = value.Split('?', '#')[0];
        }
        else
        {
            return null;
        }

        string[] parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts switch
        {
            ["note", var id] when Guid.TryParse(id, CultureInfo.InvariantCulture, out Guid noteId) => new NoteLink.Own(noteId),
            ["p", var handle, var slug] when Segment().IsMatch(handle) && Segment().IsMatch(slug) => new NoteLink.Public(handle, slug),
            ["s", var token] when Segment().IsMatch(token) => new NoteLink.Shared(token),
            _ => null,
        };
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{1,200}$")]
    private static partial Regex Segment();
}
