using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NoteLab.Application;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>"Organizar com IA" (ADR-037): validação da resposta, custo e adaptadores (sem chamadas reais).</summary>
public class NoteAiOrganizerTests
{
    private const string Note = """
        # Economia 2009 a 2018

        Levantamento de **indicadores** oficiais.

        - PIB real
        - Inflação

        [[grafico:0]]

        | Ano | PIB |
        | --- | --- |
        | 2014 | 0,5 |
        | 2015 | -3,5 |
        """;

    private static string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public void Fake_organizer_round_trip_is_valid()
    {
        string answer = NoteAiOrganizer.FakeOrganize(Note);
        OrganizedNote result = NoteAiOrganizer.Parse(answer);
        NoteAiOrganizer.Validate(Note, result);

        Assert.Equal(["heading", "paragraph", "list", "chart", "table"], result.Blocks.Select(b => b.Type));
        Assert.Equal(1, result.Blocks[0].Level);
        Assert.Equal("PIB real\nInflação", result.Blocks[2].Text);
        Assert.Equal(0, result.Blocks[3].Ref);
    }

    [Fact]
    public void Parse_accepts_code_fences_and_clamps_levels()
    {
        string answer = "```json\n" + Json(new { title = "T", blocks = new object[] { new { type = "heading", level = 9, text = "A" } } }) + "\n```";
        OrganizedNote result = NoteAiOrganizer.Parse(answer);
        Assert.Equal("T", result.Title);
        Assert.Equal(3, result.Blocks[0].Level);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{\"blocks\": []}")]
    [InlineData("{\"blocks\": [{\"type\": \"script\", \"text\": \"x\"}]}")]
    [InlineData("{\"blocks\": [{\"type\": \"chart\"}]}")]
    public void Parse_rejects_invalid_answers(string answer)
    {
        Assert.Throws<OrganizeRejectedException>(() => NoteAiOrganizer.Parse(answer));
    }

    private static OrganizedNote Organized(params OrganizedBlock[] blocks) => new(null, blocks);

    private const string Simple = "Primeira frase com várias palavras importantes aqui.\n\nSegunda frase também com várias palavras.\n\n[[grafico:0]]";

    [Fact]
    public void Validate_rejects_omitted_content()
    {
        var result = Organized(new OrganizedBlock("paragraph", "Primeira frase com várias palavras importantes aqui."), new OrganizedBlock("chart", Ref: 0));
        var ex = Assert.Throws<OrganizeRejectedException>(() => NoteAiOrganizer.Validate(Simple, result));
        Assert.Contains("omitiu", ex.Message);
    }

    [Fact]
    public void Validate_rejects_invented_content()
    {
        var result = Organized(
            new OrganizedBlock("paragraph", "Primeira frase com várias palavras importantes aqui."),
            new OrganizedBlock("paragraph", "Segunda frase também com várias palavras. Conclusão inventada pela máquina."),
            new OrganizedBlock("chart", Ref: 0));
        var ex = Assert.Throws<OrganizeRejectedException>(() => NoteAiOrganizer.Validate(Simple, result));
        Assert.Contains("acrescentou", ex.Message);
    }

    [Fact]
    public void Validate_rejects_lost_or_duplicated_charts()
    {
        var lost = Organized(new OrganizedBlock("paragraph", Simple.Replace("[[grafico:0]]", "")));
        Assert.Throws<OrganizeRejectedException>(() => NoteAiOrganizer.Validate(Simple, lost));

        var duplicated = Organized(new OrganizedBlock("paragraph", Simple.Replace("[[grafico:0]]", "")), new OrganizedBlock("chart", Ref: 0), new OrganizedBlock("chart", Ref: 0));
        Assert.Throws<OrganizeRejectedException>(() => NoteAiOrganizer.Validate(Simple, duplicated));
    }

    [Fact]
    public void Validate_accepts_restructuring_without_markup()
    {
        var result = new OrganizedNote("Economia 2009 a 2018", [
            new OrganizedBlock("paragraph", "Levantamento de indicadores oficiais."),
            new OrganizedBlock("list", "PIB real\nInflação"),
            new OrganizedBlock("chart", Ref: 0),
            new OrganizedBlock("table", "| Ano | PIB |\n| --- | --- |\n| 2014 | 0,5 |\n| 2015 | -3,5 |"),
        ]);
        NoteAiOrganizer.Validate(Note, result);
    }

    [Fact]
    public void Cost_requires_configured_prices()
    {
        IConfiguration empty = new ConfigurationBuilder().Build();
        Assert.Null(AiPricing.Cost(empty, "chatgpt", 1000, 1000));

        IConfiguration priced = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Pricing:chatgpt:InputPerMillionUsd"] = "0.15",
            ["Ai:Pricing:chatgpt:OutputPerMillionUsd"] = "0.60",
        }).Build();
        Assert.Equal(0.00075m, AiPricing.Cost(priced, "chatgpt", 1000, 1000));
    }

    [Fact]
    public async Task Fake_provider_completes_only_organize_prompts()
    {
        var fake = new FakeAiService();
        AiCompletion done = await fake.CompleteAsync(NoteAiOrganizer.SystemPrompt, NoteAiOrganizer.UserPrompt("Olá mundo"), 100, default);
        Assert.Equal("Olá mundo", NoteAiOrganizer.Parse(done.Text).Blocks.Single().Text);
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.CompleteAsync("s", "outra tarefa", 100, default));
    }

    private sealed class CaptureHandler(string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public Uri? Url { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    // Observação: o adaptador OpenAI-compatível depende de AiSettings (lê SystemSettings no banco); seu CompleteAsync
    // reaproveita o mesmo envio/parse do AnswerAsync já coberto pela suíte HTTP "admin" e por AiAdaptersTests.Parse.

    [Fact]
    public async Task Gemini_complete_asks_json_without_search()
    {
        var handler = new CaptureHandler("""{ "candidates": [ { "content": { "parts": [ { "text": "{\"blocks\":[]}" } ] } } ], "usageMetadata": { "promptTokenCount": 30, "candidatesTokenCount": 5 } }""");
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "chave-teste",
            ["Gemini:BaseUrl"] = "http://fake.local",
        }).Build();
        var service = new GeminiAiService(config, new HttpClient(handler));

        AiCompletion result = await service.CompleteAsync("sistema", "usuário", 500, default);

        Assert.Contains("application/json", handler.Body);
        Assert.Contains("systemInstruction", handler.Body);
        Assert.DoesNotContain("google_search", handler.Body);
        Assert.Equal("{\"blocks\":[]}", result.Text);
        Assert.Equal(30, result.InputTokens);
    }
}
