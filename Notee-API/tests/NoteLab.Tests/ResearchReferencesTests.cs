using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

/// <summary>ADR-045: resposta de dúvida com [n] ligados a fontes conferidas ou a origens declaradas (sem link).</summary>
public class ResearchReferencesTests
{
    private static ResolvedSource Found(string title, string? url = null, string? doi = null, string? isbn = null) =>
        new("article", title, [], 2020, null, doi, isbn, url, null, "test", null);

    [Fact]
    public void Parse_reads_answer_and_references_and_tolerates_plain_text()
    {
        (string text, List<DeclaredReference> refs) = ResearchAnswerFormat.Parse("""
            ```json
            {"answer":"O PIB caiu [1]. A inflação subiu [2].",
             "references":[{"n":1,"identifier":"10.1000/abc","label":"Artigo X"},
                           {"n":2,"identifier":null,"label":"IBGE, Censo 2022"},
                           {"n":2,"identifier":"dup","label":"dup"},{"n":0,"label":"inválido"},{"n":3,"label":"  "}]}
            ```
            """);

        Assert.Equal("O PIB caiu [1]. A inflação subiu [2].", text);
        Assert.Equal([new DeclaredReference(1, "10.1000/abc", "Artigo X"), new DeclaredReference(2, null, "IBGE, Censo 2022")], refs);

        (string plain, List<DeclaredReference> none) = ResearchAnswerFormat.Parse("Resposta sem JSON.");
        Assert.Equal("Resposta sem JSON.", plain);
        Assert.Empty(none);
    }

    [Fact]
    public void Renumber_maps_markers_and_removes_unknown_ones()
    {
        string text = ResearchAnswerFormat.Renumber("A [3]. B [1]. C [9].", new Dictionary<int, int> { [3] = 1, [1] = 2 });
        Assert.Equal("A [1]. B [2]. C.", text);
    }

    [Fact]
    public async Task Assemble_links_found_references_and_keeps_unfound_as_unverified_origins()
    {
        var answer = new AiAnswer("Dado A [1]. Dado B [2]. Dado C [3]. Dado A de novo [4].", [])
        {
            DeclaredReferences =
            [
                new DeclaredReference(1, "10.1000/abc", "Artigo X"),
                new DeclaredReference(2, "https://inexistente.example", "Site Y"),
                new DeclaredReference(3, null, "IBGE, Censo 2022"),
                new DeclaredReference(4, "10.1000/abc", "Artigo X"),
            ],
        };
        var calls = new List<string>();

        (string text, List<ResearchSourceDraft> sources) = await ResearchReferences.AssembleAsync(answer, (id, _) =>
        {
            calls.Add(id);
            return Task.FromResult(id == "10.1000/abc" ? Found("Artigo X conferido", doi: "10.1000/abc") : null);
        }, CancellationToken.None);

        Assert.Equal(
            [
                new ResearchSourceDraft("Artigo X conferido", "https://doi.org/10.1000/abc", true),
                new ResearchSourceDraft("Site Y", null, false),
                new ResearchSourceDraft("IBGE, Censo 2022", null, false),
            ],
            sources);
        // Referência sem identificador não é consultada; a repetida reaproveita a posição 1.
        Assert.Equal(["10.1000/abc", "https://inexistente.example", "10.1000/abc"], calls);
        Assert.Equal("Dado A [1]. Dado B [2]. Dado C [3]. Dado A de novo [1].", text);
    }

    [Fact]
    public async Task Assemble_keeps_provider_search_links_first_and_text_untouched_without_declared_references()
    {
        var answer = new AiAnswer("Resposta.", [new AiSource("Busca", "https://ex.org/a")]);
        (string text, List<ResearchSourceDraft> sources) = await ResearchReferences.AssembleAsync(answer,
            (_, _) => throw new InvalidOperationException("não deveria consultar"), CancellationToken.None);

        Assert.Equal("Resposta.", text);
        Assert.Equal([new ResearchSourceDraft("Busca", "https://ex.org/a", true)], sources);
    }

    [Fact]
    public void LinkFor_prefers_url_then_doi_then_isbn()
    {
        Assert.Equal("https://ex.org/", ResearchReferences.LinkFor(Found("t", url: "https://ex.org", doi: "10.1/x")));
        Assert.Equal("https://doi.org/10.1/x", ResearchReferences.LinkFor(Found("t", doi: "10.1/x")));
        Assert.Equal("https://openlibrary.org/isbn/9788535902778", ResearchReferences.LinkFor(Found("t", isbn: "9788535902778")));
        Assert.Null(ResearchReferences.LinkFor(Found("t", url: "ftp://x")));
    }

    [Fact]
    public void Declared_source_has_no_link_and_is_not_verified()
    {
        ResearchSource declared = ResearchSource.Declared(Guid.NewGuid(), Guid.NewGuid(), 0, "  IBGE, Censo 2022 ");
        Assert.Null(declared.Url);
        Assert.False(declared.Verified);
        Assert.Equal("IBGE, Censo 2022", declared.Title);
        Assert.True(new ResearchSource(Guid.NewGuid(), Guid.NewGuid(), 0, "t", "https://ex.org").Verified);
        Assert.Throws<DomainException>(() => ResearchSource.Declared(Guid.NewGuid(), Guid.NewGuid(), 0, " "));
    }
}
