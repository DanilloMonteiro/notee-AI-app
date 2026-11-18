using System.Text.Json.Nodes;
using NoteLab.Application;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>ADR-046: o Notee pesquisa na web (DeepSeek Search) e a IA responde só com os resultados.</summary>
public class WebSearchTests
{
    private static readonly WebSearchOptions Options = new(true, 1.5, 6, 2, 500, 0.75m);

    private static WebSearchResult R(string url, double score, string summary = "resumo", int authority = 2) =>
        new($"Título {new Uri(url).Host}", url, "Site", "trecho", summary, new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), authority, score);

    private sealed class StubSearch(IReadOnlyList<WebSearchResult>? results = null, bool available = true, Exception? error = null) : IWebSearch
    {
        public List<string> Queries { get; } = [];
        public bool Available => available;

        public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, string? intent, CancellationToken ct)
        {
            Queries.Add(query);
            return error is not null ? Task.FromException<IReadOnlyList<WebSearchResult>>(error) : Task.FromResult(results ?? []);
        }
    }

    [Fact]
    public void Query_fits_128_chars_with_language_filter_and_keeps_full_intent()
    {
        (string query, string intent) = WebSearchQuery.Build(new string('a', 200) + "   \n  fim");
        Assert.True(query.Length <= WebSearchOptions.MaxQueryLength);
        Assert.EndsWith(" lang:pt,en", query);
        Assert.Equal(WebSearchOptions.MaxQueryLength, intent.Length);
        Assert.Equal("Qual a inflação? lang:pt,en", WebSearchQuery.Build("  Qual   a inflação? ").Query);
    }

    [Fact]
    public void Select_applies_min_score_per_site_limit_order_and_max()
    {
        var results = new[]
        {
            R("https://a.org/1", 1.6), R("https://www.a.org/2", 2.9), R("https://a.org/3", 2.5),
            R("https://b.org/1", 1.4), R("https://c.org/1", 2.0, summary: ""), R("https://a.org/2", 2.9),
        };
        var c = results[4] with { Snippet = "" };
        List<WebSearchResult> picked = WebSearchQuery.Select(results.Take(4).Append(c).Append(results[5]), Options);

        // a.org/2 com e sem www conta uma vez; a.org: no máximo 2; b.org abaixo de 1,5; c.org sem resumo nem trecho.
        Assert.Equal(["https://www.a.org/2", "https://a.org/3"], picked.Select(r => r.Url));
        Assert.Equal(0.00075m, Options.CostOf(1));
    }

    [Fact]
    public void Parse_reads_deepseek_results_and_skips_invalid_items()
    {
        JsonNode json = JsonNode.Parse("""
            {"results":[
              {"rerank_score":2.4,"title":"Artigo","url":"https://ex.org/a","snippet":"s","summary":"resumo longo",
               "content":"","site_name":"Exemplo","site_icon":null,"date":"2026-06-01T00:00:00+00:00","authority":3},
              {"rerank_score":2.0,"title":"","url":"https://ex.org/sem-titulo"},
              {"rerank_score":2.0,"title":"FTP","url":"ftp://ex.org/x"},
              {"title":"Sem nota","url":"https://ex.org/b","date":null,"authority":-1,"site_name":""}
            ]}
            """)!;

        IReadOnlyList<WebSearchResult> results = DeepSeekWebSearch.Parse(json);

        Assert.Equal(2, results.Count);
        Assert.Equal(new WebSearchResult("Artigo", "https://ex.org/a", "Exemplo", "s", "resumo longo",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), 3, 2.4), results[0]);
        Assert.Null(results[1].SiteName);
        Assert.Null(results[1].PublishedAt);
        Assert.Equal(0, results[1].RerankScore);
        Assert.Empty(DeepSeekWebSearch.Parse(JsonNode.Parse("""{"results":[]}""")));
    }

    [Fact]
    public void Finalize_keeps_only_cited_results_in_citation_order_and_drops_invalid_markers()
    {
        var results = new[] { R("https://a.org", 2), R("https://b.org", 2), R("https://c.org", 2, authority: 4) };
        (string text, List<ResearchSourceDraft> sources) = GroundedAnswer.Finalize("X [3]. Y [1][3]. Z [9].", results);

        Assert.Equal("X [1]. Y [2][1]. Z.", text);
        Assert.Equal(["https://c.org/", "https://a.org/"], sources.Select(s => new Uri(s.Url!).ToString()));
        Assert.All(sources, s => Assert.True(s.Verified));
        Assert.Equal(4, sources[0].Authority);
        Assert.Equal("Site", sources[0].SiteName);
    }

    [Fact]
    public void ParseAnswer_reads_json_and_falls_back_to_plain_text()
    {
        Assert.Equal("Resposta [1].", GroundedAnswer.ParseAnswer("```json\n{\"answer\":\" Resposta [1]. \"}\n```"));
        Assert.Equal("Texto solto [1].", GroundedAnswer.ParseAnswer("Texto solto [1]."));
    }

    [Fact]
    public async Task TryAnswer_searches_sends_only_results_and_records_one_billed_search()
    {
        var search = new StubSearch([R("https://a.org", 2.5), R("https://b.org", 1.0)]);
        string? sentUser = null;
        GroundedOutcome? outcome = await GroundedAnswer.TryAnswerAsync(search, Options, (system, user, max, _) =>
        {
            sentUser = user;
            Assert.Equal(GroundedAnswer.System, system);
            return Task.FromResult(new AiCompletion("{\"answer\":\"Sim [1].\"}", 100, 20));
        }, "A inflação caiu?", "inflação", CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.Equal("Sim [1].", outcome!.Text);
        Assert.Equal("https://a.org", Assert.Single(outcome.Sources).Url);
        Assert.Equal(1, outcome.WebSearches);
        Assert.Equal((100, 20), (outcome.InputTokens, outcome.OutputTokens));
        Assert.Equal(["A inflação caiu? lang:pt,en"], search.Queries);
        // Só o resultado que passou no filtro vai para a IA, e sem o link (só título/site/data/resumo).
        Assert.Contains("a.org", sentUser!);
        Assert.DoesNotContain("b.org", sentUser!);
        Assert.DoesNotContain("https://", sentUser!);
    }

    [Fact]
    public async Task TryAnswer_returns_null_to_fall_back_when_search_is_off_unavailable_failing_or_weak()
    {
        Func<string, string, int, CancellationToken, Task<AiCompletion>> never =
            (_, _, _, _) => throw new InvalidOperationException("não deveria chamar a IA");

        Assert.Null(await GroundedAnswer.TryAnswerAsync(new StubSearch([R("https://a.org", 3)]), Options with { Enabled = false }, never, "q", "", default));
        Assert.Null(await GroundedAnswer.TryAnswerAsync(new StubSearch(available: false), Options, never, "q", "", default));
        Assert.Null(await GroundedAnswer.TryAnswerAsync(new StubSearch(error: new WebSearchException("sem saldo", 402)), Options, never, "q", "", default));
        Assert.Null(await GroundedAnswer.TryAnswerAsync(new StubSearch([R("https://a.org", 1.2)]), Options, never, "q", "", default));
        Assert.Null(await GroundedAnswer.TryAnswerAsync(new StubSearch([]), Options, never, "q", "", default));
    }

    [Fact]
    public void Compose_uses_search_results_as_sources_without_resolving_and_drops_unknown_numbers()
    {
        var results = new[] { R("https://ibge.gov.br/pib", 2.8), R("https://bcb.gov.br/ipca", 2.6) };
        string prompt = NoteComposer.UserPrompt(ComposeMode.Generate, "PIB", "explicar", null, null, [], [], results);
        Assert.Contains("\"searchResults\"", prompt);
        Assert.DoesNotContain("\"url\"", prompt); // a IA recebe título/site/data/resumo, não o link
        Assert.DoesNotContain("searchResults", NoteComposer.UserPrompt(ComposeMode.Generate, "PIB", "explicar", null, null, [], []));

        string json = """
            {"blocks":[{"type":"paragraph","text":"O PIB cresceu 2,9%. O IPCA subiu."}],
             "sources":[{"result":1,"reason":"dados","supports":["O PIB cresceu 2,9%."]},
                        {"result":7,"reason":"inexistente"},
                        {"result":2,"reason":"dados","supports":["O IPCA subiu."]}]}
            """;
        ComposeDraft draft = NoteComposer.Parse(json, 0, [], [], searchResultCount: results.Length);
        Assert.Equal(["busca:1", "busca:2"], draft.Sources.Select(s => s.Identifier));

        var resolved = new Dictionary<string, ResolvedSource>
        {
            ["busca:1"] = NoteComposer.AsResolvedSource(results[0]),
            ["busca:2"] = NoteComposer.AsResolvedSource(results[1]),
        };
        ComposeResult result = NoteComposer.Assemble(draft, [], resolved);
        Assert.Equal(["https://ibge.gov.br/pib", "https://bcb.gov.br/ipca"], result.Sources.Select(s => s.Resolved.Url));
        Assert.Equal(["O PIB cresceu 2,9%."], result.Sources[0].Supports);
        Assert.Empty(result.Origins!);

        // Sem busca, "result" é ignorado (não há resultados para apontar).
        Assert.Empty(NoteComposer.Parse(json, 0, [], []).Sources);
    }
}
