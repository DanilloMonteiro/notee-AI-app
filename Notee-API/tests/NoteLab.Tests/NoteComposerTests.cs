using System.Text.Json;
using System.Text.Json.Nodes;
using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

/// <summary>Criação de nota assistida por IA (ADR-044): leitura da resposta, gráficos com fonte, Fake e links.</summary>
public class NoteComposerTests
{
    private static readonly ResolvedSource Ibge = new("website", "Contas Nacionais — IBGE", [], 2020, "IBGE", null, null,
        "https://www.ibge.gov.br/contas", null, "url", null);

    private static string Chart(string source = "https://www.ibge.gov.br/contas", string labels = "[\"2014\",\"2015\"]",
        string values = "[0.5,-3.5]") =>
        $$"""{"chartType":"bar","title":"PIB","labels":{{labels}},"series":[{"name":"PIB real","values":{{values}}}],"sourceIdentifier":"{{source}}","sourceReason":"série oficial"}""";

    [Fact]
    public void Parse_reuses_taxonomy_normalization_and_limits_sources()
    {
        var category = Category.Create("owner", "Economia");
        var tag = Tag.Create("owner", "Inflação");
        string sources = string.Join(',', Enumerable.Range(1, 7).Select(i => $$"""{"identifier":"https://ex.org/{{i}}","reason":"r"}"""));
        string json = $$"""
            {"title":null,"blocks":[{"type":"paragraph","text":"Texto"}],
             "category":{"name":"economia","reason":"tema"},"tags":[{"name":"inflacao","reason":"texto"}],
             "sources":[{"identifier":"https://ex.org/1","reason":"dup"},{{sources}}]}
            """;

        ComposeDraft draft = NoteComposer.Parse(json, 0, [category], [tag]);

        Assert.Equal(category.Id, draft.Taxonomy.Category?.ExistingId);
        Assert.Equal(tag.Id, draft.Taxonomy.Tags.Single().ExistingId);
        Assert.Equal(NoteComposer.MaxSources, draft.Sources.Count);
        Assert.Equal(draft.Sources.Count, draft.Sources.Select(s => s.Identifier).Distinct().Count());
    }

    [Fact]
    public void Parse_discards_charts_without_source_or_with_invalid_schema()
    {
        string json = $$"""
            {"blocks":[{"type":"paragraph","text":"Intro"},{"type":"newchart","ref":0},{"type":"newchart","ref":1},
                       {"type":"newchart","ref":2},{"type":"chart","ref":7}],
             "charts":[{{Chart(source: "")}},{{Chart(values: "[1]")}},{{Chart()}}]}
            """;

        ComposeDraft draft = NoteComposer.Parse(json, 0, [], []);

        Assert.Equal(2, draft.DiscardedCharts);
        ComposeChartDraft chart = Assert.Single(draft.NewCharts);
        Assert.Equal("https://www.ibge.gov.br/contas", chart.SourceIdentifier);
        // Referência a gráfico base inexistente (ref 7) some; só o gráfico válido continua no texto.
        Assert.Equal(["paragraph", "newchart"], draft.Blocks.Select(b => b.Type));
        Assert.Equal(0, draft.Blocks[1].Ref);
    }

    [Fact]
    public void Parse_keeps_only_supports_that_exist_in_the_generated_text()
    {
        string json = """
            {"blocks":[{"type":"paragraph","text":"O PIB caiu 3,5% em 2015. A inflação subiu."}],
             "sources":[{"identifier":"https://www.ibge.gov.br/contas","reason":"série oficial",
                         "supports":["O PIB caiu 3,5% em 2015.","trecho que não está na nota","O PIB caiu 3,5% em 2015.","ok"]}],
             "origins":[{"label":"Banco Central, Relatório de Inflação","supports":["A inflação subiu."]},
                        {"label":"  ","supports":[]},{"label":"Banco Central, Relatório de Inflação","supports":[]}]}
            """;

        ComposeDraft draft = NoteComposer.Parse(json, 0, [], []);

        // Só trechos que existem no texto, sem duplicata e com tamanho mínimo.
        Assert.Equal(["O PIB caiu 3,5% em 2015."], Assert.Single(draft.Sources).Supports);
        ComposeOrigin origin = Assert.Single(draft.Origins!);
        Assert.Equal("Banco Central, Relatório de Inflação", origin.Label);
        Assert.Equal(["A inflação subiu."], origin.Supports);
        Assert.False(origin.FromUnresolvedIdentifier);
    }

    [Fact]
    public void Parse_limits_origins_and_accepts_responses_without_the_new_fields()
    {
        string origins = string.Join(',', Enumerable.Range(1, 8).Select(i => $$"""{"label":"Origem {{i}}"}"""));
        ComposeDraft draft = NoteComposer.Parse($$"""{"blocks":[{"type":"paragraph","text":"Texto"}],"origins":[{{origins}}]}""", 0, [], []);
        Assert.Equal(NoteComposer.MaxOrigins, draft.Origins!.Count);
        Assert.All(draft.Origins, o => Assert.Empty(o.Supports));

        ComposeDraft old = NoteComposer.Parse("""{"blocks":[{"type":"paragraph","text":"Texto"}],"sources":[{"identifier":"https://ex.org","reason":"r"}]}""", 0, [], []);
        Assert.Empty(old.Origins!);
        Assert.Empty(Assert.Single(old.Sources).Supports!);
    }

    [Fact]
    public void Assemble_keeps_supports_and_turns_unresolved_identifier_into_unverified_origin()
    {
        string json = """
            {"blocks":[{"type":"paragraph","text":"O PIB caiu 3,5% em 2015. Dado de outra fonte."}],
             "sources":[{"identifier":"https://www.ibge.gov.br/contas","reason":"r","supports":["O PIB caiu 3,5% em 2015."]},
                        {"identifier":"10.9999/nao-existe","reason":"r","supports":["Dado de outra fonte."]}]}
            """;
        ComposeDraft draft = NoteComposer.Parse(json, 0, [], []);
        var resolved = new Dictionary<string, ResolvedSource>(StringComparer.OrdinalIgnoreCase) { ["https://www.ibge.gov.br/contas"] = Ibge };

        ComposeResult result = NoteComposer.Assemble(draft, [], resolved);

        Assert.Equal(["O PIB caiu 3,5% em 2015."], Assert.Single(result.Sources).Supports);
        Assert.Equal(1, result.DiscardedSources);
        ComposeOrigin origin = Assert.Single(result.Origins!);
        Assert.Equal("10.9999/nao-existe", origin.Label);
        Assert.True(origin.FromUnresolvedIdentifier);
        Assert.Equal(["Dado de outra fonte."], origin.Supports);
    }

    [Fact]
    public void Parse_rejects_response_without_text()
    {
        Assert.Throws<OrganizeRejectedException>(() => NoteComposer.Parse("não é json", 0, [], []));
        Assert.Throws<OrganizeRejectedException>(() => NoteComposer.Parse("""{"blocks":[]}""", 0, [], []));
    }

    [Fact]
    public void Assemble_keeps_chart_only_when_its_source_resolves()
    {
        string json = $$"""
            {"blocks":[{"type":"paragraph","text":"Intro"},{"type":"newchart","ref":0},{"type":"newchart","ref":1}],
             "charts":[{{Chart()}},{{Chart(source: "https://inexistente.example/x")}}],
             "sources":[{"identifier":"https://nao-existe.example","reason":"r"}]}
            """;
        ComposeDraft draft = NoteComposer.Parse(json, 0, [], []);
        var resolved = new Dictionary<string, ResolvedSource>(StringComparer.OrdinalIgnoreCase) { ["https://www.ibge.gov.br/contas"] = Ibge };

        ComposeResult result = NoteComposer.Assemble(draft, [], resolved);

        Assert.Equal(1, result.DiscardedCharts);
        Assert.Equal(1, result.DiscardedSources);
        ComposeChartOut chart = Assert.Single(result.Charts);
        Assert.Equal("generated", chart.Origin);
        Assert.Equal("Contas Nacionais — IBGE", chart.Chart["sources"]![0]!["label"]!.GetValue<string>());
        Assert.Equal("https://www.ibge.gov.br/contas", chart.Chart["sources"]![0]!["url"]!.GetValue<string>());
        // A fonte do gráfico também entra na lista de fontes da nota.
        Assert.Equal("https://www.ibge.gov.br/contas", result.Sources[chart.SourceIndex!.Value].Identifier);
        // O bloco salvo passa a ser válido para NoteContent.
        NoteContent.Validate(new JsonArray(chart.Chart.DeepClone()).ToJsonString());
        Assert.Equal(["paragraph", "chart"], result.Blocks.Select(b => b.Type));
    }

    [Fact]
    public void Base_charts_are_preserved_untouched()
    {
        string baseContent = """
            [{"id":"h","type":"heading","level":1,"text":"Economia"},
             {"id":"c1","type":"chart","schemaVersion":1,"chartType":"line","title":"Inflação","labels":["a","b"],
              "series":[{"name":"IPCA","values":[1,2]}],"sources":[{"label":"BCB","url":"https://bcb.gov.br"}]}]
            """;
        ComposeBaseText baseText = NoteComposer.BaseText(baseContent);
        Assert.Equal("# Economia\n\n[[grafico:0]]", baseText.Text);

        ComposeDraft draft = NoteComposer.Parse("""{"blocks":[{"type":"paragraph","text":"Novo"},{"type":"chart","ref":0}]}""", baseText.Charts.Count, [], []);
        ComposeResult result = NoteComposer.Assemble(draft, baseText.Charts, new Dictionary<string, ResolvedSource>());

        ComposeChartOut chart = Assert.Single(result.Charts);
        Assert.Equal("base", chart.Origin);
        Assert.True(JsonNode.DeepEquals(baseText.Charts[0], chart.Chart));
        Assert.Equal(0, result.DiscardedCharts);
    }

    [Fact]
    public void Fake_is_deterministic_and_never_suggests_sources_or_new_charts()
    {
        string prompt = NoteComposer.UserPrompt(ComposeMode.Generate, "Inflação no Brasil",
            "Explicar a inflação na economia do Brasil. Comparar inflação e juros; citar efeitos na renda.", null, null,
            [Category.Create("o", "Economia")], []);

        string first = NoteComposer.FakeCompletion(prompt);
        string second = NoteComposer.FakeCompletion(prompt);
        ComposeDraft draft = NoteComposer.Parse(first, 0, [Category.Create("o", "Economia")], []);

        Assert.Equal(first, second);
        Assert.Empty(draft.Sources);
        Assert.Empty(draft.NewCharts);
        Assert.Contains(draft.Blocks, b => b.Type == "list");
        Assert.Equal("Economia", draft.Taxonomy.Category?.Name);
    }

    [Fact]
    public void Fake_rewrite_keeps_base_chart_references()
    {
        ComposeBaseText baseText = NoteComposer.BaseText("""[{"id":"p","type":"paragraph","text":"Texto base"},{"id":"c","type":"chart","schemaVersion":1,"chartType":"bar","title":"x","labels":["a"],"series":[{"name":"s","values":[1]}]}]""");
        string prompt = NoteComposer.UserPrompt(ComposeMode.Rewrite, "Cópia", "Deixar mais curto", "Original", baseText, [], []);

        ComposeDraft draft = NoteComposer.Parse(NoteComposer.FakeCompletion(prompt), baseText.Charts.Count, [], []);

        Assert.Contains(draft.Blocks, b => b.Type == "chart" && b.Ref == 0);
        Assert.Contains(draft.Blocks, b => b.Text == "Deixar mais curto");
    }

    [Theory]
    [InlineData("https://notee.app/note/8f9b1a8e-2c3d-4e5f-8a9b-1c2d3e4f5a6b", typeof(NoteLink.Own))]
    [InlineData("/p/maria/economia-2015", typeof(NoteLink.Public))]
    [InlineData("http://localhost:5173/s/AbC_123-xyz?x=1", typeof(NoteLink.Shared))]
    public void Link_parser_recognizes_supported_links(string input, Type expected) =>
        Assert.IsType(expected, NoteLinkParser.Parse(input));

    [Theory]
    [InlineData("")]
    [InlineData("note/123")]
    [InlineData("/note/nao-e-guid")]
    [InlineData("ftp://x/s/abc")]
    [InlineData("/p/maria")]
    [InlineData("https://example.com/qualquer")]
    public void Link_parser_rejects_other_inputs(string input) => Assert.Null(NoteLinkParser.Parse(input));

    [Fact]
    public void User_prompt_carries_only_declared_scope()
    {
        string prompt = NoteComposer.UserPrompt(ComposeMode.Generate, "T", "D", null, null, [Category.Create("o", "C")], [Tag.Create("o", "G")]);
        using JsonDocument doc = JsonDocument.Parse(prompt);

        Assert.Equal(["mode", "title", "instructions", "baseNote", "categories", "tags"],
            doc.RootElement.EnumerateObject().Select(p => p.Name));
    }
}
