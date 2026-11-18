using System.Text.Json.Nodes;
using NoteLab.Application;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>ADR-047: artigos acadêmicos (OpenAlex) primeiro, internet só completa; bloqueados e confiáveis.</summary>
public class SourceSearchTests
{
    private static readonly SourceQuery Query = new("inflação Brasil", "inflation Brazil", "A inflação caiu no Brasil?");

    [Fact]
    public void OpenAlex_parse_rebuilds_abstract_prefers_doi_and_normalizes_relevance()
    {
        JsonNode json = JsonNode.Parse("""
            {"results":[
              {"id":"https://openalex.org/W1","doi":"https://doi.org/10.1/abc","display_name":"Inflação no Brasil",
               "publication_date":"2023-04-10","relevance_score":200,
               "authorships":[{"author":{"display_name":"Ana Souza"}},{"author":{"display_name":"João Lima"}}],
               "primary_location":{"landing_page_url":"https://revista.br/a","source":{"display_name":"Revista de Economia"}},
               "open_access":{"is_oa":true,"oa_url":"https://revista.br/a.pdf"},
               "abstract_inverted_index":{"caiu":[1],"A":[0],"inflação":[2],"em":[3],"2023":[4]}},
              {"id":"https://openalex.org/W2","doi":null,"display_name":"Sem DOI","publication_date":null,"relevance_score":50,
               "authorships":[],"primary_location":{"landing_page_url":"https://repo.org/x","source":null},
               "open_access":{"oa_url":null},"abstract_inverted_index":{"Resumo":[0],"curto":[1]}},
              {"display_name":"Sem resumo","doi":"https://doi.org/10.1/zz","abstract_inverted_index":null},
              {"display_name":"","doi":"https://doi.org/10.1/sem-titulo","abstract_inverted_index":{"x":[0]}}
            ]}
            """)!;

        IReadOnlyList<SourceHit> hits = OpenAlexSearch.Parse(json);

        Assert.Equal(2, hits.Count);
        SourceHit a = hits[0];
        Assert.Equal(("Inflação no Brasil", "https://doi.org/10.1/abc", "10.1/abc"), (a.Title, a.Url, a.Doi));
        Assert.Equal("A caiu inflação em 2023", a.Excerpt);
        Assert.Equal(("Revista de Economia", SourceOrigin.Academic, "openalex"), (a.SiteName, a.Origin, a.Provider));
        Assert.Equal(["Ana Souza", "João Lima"], a.Authors);
        Assert.Equal(new DateTimeOffset(2023, 4, 10, 0, 0, 0, TimeSpan.Zero), a.PublishedAt);
        Assert.Equal(1, a.Score);
        // Sem DOI nem cópia aberta: link da página do editor; nota relativa à maior da resposta.
        Assert.Equal(("https://repo.org/x", null, 0.25), (hits[1].Url, hits[1].SiteName, hits[1].Score));
        Assert.Empty(OpenAlexSearch.Parse(JsonNode.Parse("""{"results":[]}""")));
    }

    [Fact]
    public void Rules_normalize_domains_and_match_subdomains()
    {
        Assert.Equal("scielo.br", SourceRules.NormalizeDomain(" https://www.SciELO.br/j/abc?x=1 "));
        Assert.Equal("pt.wikipedia.org", SourceRules.NormalizeDomain("pt.wikipedia.org"));
        Assert.Null(SourceRules.NormalizeDomain("não é site"));
        Assert.Null(SourceRules.NormalizeDomain("localhost"));

        var rules = new SourceRules(["wikipedia.org"], ["ibge.gov.br"]);
        Assert.True(rules.IsBlocked("https://pt.wikipedia.org/wiki/Inflação"));
        Assert.False(rules.IsBlocked("https://notwikipedia.org/x"));
        Assert.True(rules.IsTrusted("https://www.ibge.gov.br/pib"));
    }

    [Fact]
    public void Mix_drops_blocked_puts_trusted_first_with_lower_threshold_and_dedupes_by_doi()
    {
        var rules = new SourceRules(SourceSearchOptions.DefaultBlocked, ["ibge.gov.br"]);
        var hits = new[]
        {
            Hits.Web("https://pt.wikipedia.org/wiki/PIB", 3.0),
            Hits.Web("https://ex.org/a", 2.4),
            Hits.Web("https://ibge.gov.br/pib", 1.0), // abaixo de 1,5, mas confiável (nota mínima pela metade)
            Hits.Web("https://ibge.gov.br/fraco", 0.3),
        };
        List<SourceHit> picked = SourceMix.Select(hits, 0.5, 2, 6, rules);
        Assert.Equal(["https://ibge.gov.br/pib", "https://ex.org/a"], picked.Select(h => h.Url));
        Assert.True(picked[0].Trusted);
        Assert.False(picked[1].Trusted);

        List<SourceHit> articles = SourceMix.Select([Hits.Article("10.1/a", 0.9), Hits.Article("10.1/a", 0.8), Hits.Article("10.1/b", 0.2)],
            0.35, 6, 6, SourceRules.Empty);
        Assert.Equal(["10.1/a"], articles.Select(a => a.Doi));

        Assert.Equal(75, SourceMix.AcademicPercent([SourceOrigin.Academic, SourceOrigin.Academic, SourceOrigin.Academic, SourceOrigin.Web]));
        Assert.Null(SourceMix.AcademicPercent([]));
    }

    [Fact]
    public async Task Mixed_uses_only_articles_when_there_are_enough()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic,
            [Hits.Article("10.1/a", 1), Hits.Article("10.1/b", 0.8), Hits.Article("10.1/c", 0.6), Hits.Article("10.1/d", 0.5),
             Hits.Article("10.1/e", 0.5), Hits.Article("10.1/f", 0.4)]);
        var web = new StubSourceProvider(SourceOrigin.Web, [Hits.Web("https://ex.org/a", 2.5)]);

        SourceSearchOutcome outcome = await Hits.Service(academic, web).SearchAsync(SourceMode.Mixed, Query, SourceRules.Empty, true, true, default);

        Assert.Equal(6, outcome.AcademicCount);
        Assert.Equal(0, outcome.WebCount);
        Assert.Empty(web.Queries); // artigos suficientes: a internet nem é consultada
        Assert.Equal((0, 1), (outcome.WebCalls, outcome.AcademicCalls));
        Assert.Equal("inflation Brazil", Assert.Single(academic.Queries).En);
    }

    [Fact]
    public async Task Mixed_completes_with_web_when_articles_are_few_and_retries_in_portuguese()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic, [Hits.Article("10.1/a", 1), Hits.Article("10.1/b", 0.1)]);
        var web = new StubSourceProvider(SourceOrigin.Web,
            [Hits.Web("https://ex.org/a", 2.5), Hits.Web("https://pt.wikipedia.org/wiki/X", 3), Hits.Web("https://b.org/a", 2.0)]);
        var rules = new SourceRules(SourceSearchOptions.DefaultBlocked, []);

        SourceSearchOutcome outcome = await Hits.Service(academic, web).SearchAsync(SourceMode.Mixed, Query, rules, true, true, default);

        // 1 artigo bom (o 2º está abaixo da nota relativa) + web completando, sem Wikipédia; artigos vêm primeiro.
        Assert.Equal(["https://doi.org/10.1/a", "https://ex.org/a", "https://b.org/a"], outcome.Hits.Select(h => h.Url));
        Assert.Equal(["inflation Brazil", "inflação Brasil"], academic.Queries.Select(q => q.En));
        Assert.Equal((1, 2), (outcome.WebCalls, outcome.AcademicCalls));
    }

    [Fact]
    public async Task Academic_only_never_calls_web_and_web_only_never_calls_academic()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic, []);
        var web = new StubSourceProvider(SourceOrigin.Web, [Hits.Web("https://ex.org/a", 2.5)]);
        SourceSearchService service = Hits.Service(academic, web);

        SourceSearchOutcome onlyArticles = await service.SearchAsync(SourceMode.AcademicOnly, Query, SourceRules.Empty, true, true, default);
        Assert.Empty(onlyArticles.Hits);
        Assert.Empty(web.Queries);

        academic.Queries.Clear();
        SourceSearchOutcome onlyWeb = await service.SearchAsync(SourceMode.WebOnly, Query, SourceRules.Empty, true, true, default);
        Assert.Equal(1, onlyWeb.WebCount);
        Assert.Empty(academic.Queries);
    }

    [Fact]
    public async Task Failure_of_one_origin_keeps_the_other_and_reports_it()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic, error: new SourceSearchException("Limite diário de buscas de artigos atingido.", 429));
        var web = new StubSourceProvider(SourceOrigin.Web, [Hits.Web("https://ex.org/a", 2.5)]);

        SourceSearchOutcome outcome = await Hits.Service(academic, web).SearchAsync(SourceMode.Mixed, Query, SourceRules.Empty, true, true, default);

        Assert.Equal("https://ex.org/a", Assert.Single(outcome.Hits).Url);
        Assert.Contains("Limite diário", Assert.Single(outcome.Notices));
    }

    [Fact]
    public async Task Grounded_answer_plans_bilingual_query_when_articles_are_searched()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic, [Hits.Article("10.1/a", 1), Hits.Article("10.1/b", 0.9), Hits.Article("10.1/c", 0.8)]);
        var calls = new List<string>();
        GroundedOutcome? outcome = await GroundedAnswer.TryAnswerAsync(Hits.Service(academic), SourceMode.Mixed, SourceRules.Empty, true, false,
            (system, user, max, _) =>
            {
                calls.Add(system);
                return Task.FromResult(system == SourceQueryPlanner.System
                    ? new AiCompletion("""{"pt":"inflação Brasil","en":"inflation Brazil"}""", 30, 10)
                    : new AiCompletion("""{"answer":"Caiu [2]."}""", 300, 40));
            }, "A inflação caiu no Brasil?", "", default);

        Assert.Equal([SourceQueryPlanner.System, GroundedAnswer.System], calls);
        Assert.Equal("inflation Brazil", academic.Queries[0].En);
        Assert.Equal("Caiu [1].", outcome!.Text);
        Assert.Equal(("academic", "10.1/b"), (outcome.Sources[0].Origin, outcome.Sources[0].Doi));
        // Tokens somam a consulta e a resposta; 3 artigos (< 6): também busca em português — 2 chamadas registradas.
        Assert.Equal((330, 50, 2, 0), (outcome.InputTokens, outcome.OutputTokens, outcome.AcademicSearches, outcome.WebSearches));
    }

    [Fact]
    public async Task Search_without_good_results_still_reports_query_tokens_and_calls()
    {
        var academic = new StubSourceProvider(SourceOrigin.Academic, []);
        GroundedOutcome? outcome = await GroundedAnswer.TryAnswerAsync(Hits.Service(academic), SourceMode.AcademicOnly, SourceRules.Empty, true, false,
            (system, _, _, _) => system == SourceQueryPlanner.System
                ? Task.FromResult(new AiCompletion("""{"pt":"a","en":"b"}""", 30, 10))
                : throw new InvalidOperationException("sem resultado não deveria pedir resposta"),
            "Pergunta?", "", default);

        Assert.NotNull(outcome);
        Assert.False(outcome!.Answered);
        Assert.Equal((30, 10, 2, 0), (outcome.InputTokens, outcome.OutputTokens, outcome.AcademicSearches, outcome.WebSearches));
    }

    [Fact]
    public void Planner_falls_back_to_original_text()
    {
        Assert.Equal(new SourceQuery("ok pt", "ok en", "Pergunta?"), SourceQueryPlanner.Parse("""```json {"pt":" ok   pt ","en":"ok en"} ```""", "Pergunta?"));
        Assert.Equal(new SourceQuery("Pergunta?", "Pergunta?", "Pergunta?"), SourceQueryPlanner.Parse("sem json", "Pergunta?"));
        Assert.Equal("Pergunta?", SourceQueryPlanner.Parse("""{"pt":"","en":"x"}""", "Pergunta?").Pt);
    }

    [Fact]
    public void Preferences_accept_only_known_source_modes()
    {
        var prefs = UserPreferences.Default("u1");
        Assert.Equal("mixed", prefs.SourceMode);
        prefs.SetSourceMode(" Academic ");
        Assert.Equal("academic", prefs.SourceMode);
        Assert.Throws<DomainException>(() => prefs.SetSourceMode("livros"));
        Assert.Equal(SourceMode.AcademicOnly, SourceModes.Parse(prefs.SourceMode));
        Assert.Equal("web", SourceModes.ToWire(SourceMode.WebOnly));
    }
}
