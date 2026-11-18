using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using NoteLab.Application;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>Origem de fonte de teste: devolve os resultados dados e registra as consultas.</summary>
internal sealed class StubSourceProvider(SourceOrigin origin, IReadOnlyList<SourceHit>? results = null, bool available = true, Exception? error = null)
    : ISourceSearchProvider
{
    public List<SourceQuery> Queries { get; } = [];

    public string Name => origin == SourceOrigin.Academic ? "openalex" : "deepseek-search";

    public SourceOrigin Origin => origin;

    public bool Available => available;

    public Task<IReadOnlyList<SourceHit>> SearchAsync(SourceQuery query, CancellationToken ct)
    {
        Queries.Add(query);
        return error is not null ? Task.FromException<IReadOnlyList<SourceHit>>(error) : Task.FromResult(results ?? []);
    }
}

internal static class Hits
{
    public static SourceHit Web(string url, double rerank, string excerpt = "resumo", int authority = 2) =>
        new($"Título {new Uri(url).Host}", url, excerpt, "Site", new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), [], null,
            SourceOrigin.Web, "deepseek-search", rerank / 3.0, authority);

    public static SourceHit Article(string doi, double score, string excerpt = "resumo do artigo") =>
        new($"Artigo {doi}", $"https://doi.org/{doi}", excerpt, "Revista", new DateTimeOffset(2024, 5, 2, 0, 0, 0, TimeSpan.Zero),
            ["Ana Souza", "John Smith"], doi, SourceOrigin.Academic, "openalex", score);

    public static SourceSearchService Service(params ISourceSearchProvider[] providers) =>
        new(providers, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
}

/// <summary>ADR-046: o Notee pesquisa na web (DeepSeek Search) e a IA responde só com os resultados.</summary>
public class WebSearchTests
{
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
            Hits.Web("https://a.org/1", 1.6), Hits.Web("https://www.a.org/2", 2.9), Hits.Web("https://a.org/3", 2.5),
            Hits.Web("https://b.org/1", 1.4), Hits.Web("https://c.org/1", 2.0, excerpt: ""), Hits.Web("https://a.org/2", 2.9),
        };
        List<SourceHit> picked = SourceMix.Select(results, 1.5 / 3.0, 2, 6, SourceRules.Empty);

        // a.org/2 com e sem www conta uma vez; a.org: no máximo 2; b.org abaixo de 1,5; c.org sem resumo.
        Assert.Equal(["https://www.a.org/2", "https://a.org/3"], picked.Select(r => r.Url));
        Assert.Equal(0.00075m, new WebSearchOptions(true, 1.5, 6, 2, 500, 0.75m).CostOf(1));
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
              {"title":"Sem nota","url":"https://ex.org/b","snippet":"só trecho","date":null,"authority":-1,"site_name":""}
            ]}
            """)!;

        IReadOnlyList<SourceHit> results = DeepSeekWebSearch.Parse(json);

        Assert.Equal(2, results.Count);
        Assert.Equal(("Artigo", "https://ex.org/a", "resumo longo", "Exemplo", 3), (results[0].Title, results[0].Url, results[0].Excerpt, results[0].SiteName, results[0].Authority));
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), results[0].PublishedAt);
        Assert.Equal(0.8, results[0].Score, 3);
        Assert.Equal(SourceOrigin.Web, results[0].Origin);
        Assert.Equal("só trecho", results[1].Excerpt);
        Assert.Null(results[1].SiteName);
        Assert.Null(results[1].PublishedAt);
        Assert.Equal(0, results[1].Score);
        Assert.Empty(DeepSeekWebSearch.Parse(JsonNode.Parse("""{"results":[]}""")));
    }

    [Fact]
    public void Finalize_keeps_only_cited_results_in_citation_order_and_drops_invalid_markers()
    {
        var results = new[] { Hits.Web("https://a.org", 2), Hits.Article("10.1/x", 0.9), Hits.Web("https://c.org", 2, authority: 4) };
        (string text, List<ResearchSourceDraft> sources) = GroundedAnswer.Finalize("X [3]. Y [2][3]. Z [9].", results);

        Assert.Equal("X [1]. Y [2][1]. Z.", text);
        Assert.Equal(["https://c.org", "https://doi.org/10.1/x"], sources.Select(s => s.Url));
        Assert.All(sources, s => Assert.True(s.Verified));
        Assert.Equal((4, "web", "Site"), (sources[0].Authority, sources[0].Origin, sources[0].SiteName));
        // Artigo: origem acadêmica com DOI e autores; autoridade do buscador não se aplica.
        Assert.Equal(("academic", "10.1/x", null), (sources[1].Origin, sources[1].Doi, sources[1].Authority));
        Assert.Equal(["Ana Souza", "John Smith"], sources[1].Authors!);
    }

    [Fact]
    public void ParseAnswer_reads_json_and_falls_back_to_plain_text()
    {
        Assert.Equal("Resposta [1].", GroundedAnswer.ParseAnswer("```json\n{\"answer\":\" Resposta [1]. \"}\n```"));
        Assert.Equal("Texto solto [1].", GroundedAnswer.ParseAnswer("Texto solto [1]."));
    }

    [Fact]
    public async Task TryAnswer_web_only_searches_sends_only_results_and_records_one_billed_search()
    {
        var web = new StubSourceProvider(SourceOrigin.Web, [Hits.Web("https://a.org", 2.5), Hits.Web("https://b.org", 1.0)]);
        string? sentUser = null;
        GroundedOutcome? outcome = await GroundedAnswer.TryAnswerAsync(Hits.Service(web), SourceMode.WebOnly, SourceRules.Empty, true, true,
            (system, user, max, _) =>
            {
                sentUser = user;
                Assert.Equal(GroundedAnswer.System, system); // só web: sem a chamada de palavras-chave
                return Task.FromResult(new AiCompletion("{\"answer\":\"Sim [1].\"}", 100, 20));
            }, "A inflação caiu?", "inflação", CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.Equal("Sim [1].", outcome!.Text);
        Assert.Equal("https://a.org", Assert.Single(outcome.Sources).Url);
        Assert.Equal((1, 0), (outcome.WebSearches, outcome.AcademicSearches));
        Assert.Equal((100, 20), (outcome.InputTokens, outcome.OutputTokens));
        Assert.Equal("A inflação caiu?", Assert.Single(web.Queries).Pt);
        // Só o resultado que passou no filtro vai para a IA, e sem o link (só título/site/data/resumo).
        Assert.Contains("a.org", sentUser!);
        Assert.DoesNotContain("b.org", sentUser!);
        Assert.DoesNotContain("https://", sentUser!);
    }

    [Fact]
    public async Task TryAnswer_returns_null_to_fall_back_when_search_is_off_unavailable_failing_or_weak()
    {
        // null (nada pesquisado) ou sem texto (pesquisou sem achar nada bom): os dois caem no caminho anterior.
        static void NotAnswered(GroundedOutcome? outcome) => Assert.False(outcome is { Answered: true });

        Func<string, string, int, CancellationToken, Task<AiCompletion>> never =
            (_, _, _, _) => throw new InvalidOperationException("não deveria chamar a IA");
        StubSourceProvider Web(IReadOnlyList<SourceHit>? r = null, bool available = true, Exception? error = null) =>
            new(SourceOrigin.Web, r, available, error);

        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web([Hits.Web("https://a.org", 3)])), SourceMode.None, SourceRules.Empty, true, true, never, "q", "", default));
        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web([Hits.Web("https://a.org", 3)])), SourceMode.WebOnly, SourceRules.Empty, true, false, never, "q", "", default));
        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web(available: false)), SourceMode.WebOnly, SourceRules.Empty, true, true, never, "q", "", default));
        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web(error: new SourceSearchException("sem saldo", 402))), SourceMode.WebOnly, SourceRules.Empty, true, true, never, "q", "", default));
        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web([Hits.Web("https://a.org", 1.2)])), SourceMode.WebOnly, SourceRules.Empty, true, true, never, "q", "", default));
        NotAnswered(await GroundedAnswer.TryAnswerAsync(Hits.Service(Web([])), SourceMode.WebOnly, SourceRules.Empty, true, true, never, "q", "", default));
    }

    [Fact]
    public void Compose_uses_search_results_as_sources_without_resolving_and_drops_unknown_numbers()
    {
        var results = new[] { Hits.Web("https://ibge.gov.br/pib", 2.8), Hits.Article("10.5/ipca", 0.9) };
        string prompt = NoteComposer.UserPrompt(ComposeMode.Generate, "PIB", "explicar", null, null, [], [], results);
        Assert.Contains("\"searchResults\"", prompt);
        Assert.Contains("\"article\"", prompt);
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
        Assert.Equal(["https://ibge.gov.br/pib", "https://doi.org/10.5/ipca"], result.Sources.Select(s => s.Resolved.Url));
        Assert.Equal(["O PIB cresceu 2,9%."], result.Sources[0].Supports);
        Assert.Empty(result.Origins!);
        // Artigo vira fonte "article" com DOI e autores (a checagem do Crossref pode confirmá-lo).
        Assert.Equal(("article", "10.5/ipca", "openalex"), (result.Sources[1].Resolved.Kind, result.Sources[1].Resolved.Doi, result.Sources[1].Resolved.Provider));
        Assert.Equal(["Ana Souza", "John Smith"], result.Sources[1].Resolved.Authors);
        Assert.Equal("website", result.Sources[0].Resolved.Kind);

        // Sem busca, "result" é ignorado (não há resultados para apontar).
        Assert.Empty(NoteComposer.Parse(json, 0, [], []).Sources);
    }
}
