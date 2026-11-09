using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using NoteLab.Application;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>
/// Contrato do provider Gemini com servidor HTTP falso (nenhuma chamada externa).
/// O formato segue a documentação pública de generateContent com grounding; não foi validado contra a API real.
/// </summary>
public class GeminiProviderTests
{
    private const string GroundedResponse = """
        {
          "candidates": [{
            "content": { "parts": [ { "text": "Outbox grava o evento junto com o dado." }, { "text": "Depois publica." } ] },
            "groundingMetadata": {
              "groundingChunks": [
                { "web": { "uri": "https://example.org/outbox", "title": "Transactional Outbox" } },
                { "web": { "uri": "https://example.org/outbox", "title": "Duplicada" } },
                { "web": { "uri": "javascript:alert(1)", "title": "Maliciosa" } },
                { "web": { "title": "Sem URL" } },
                { "web": { "uri": "http://example.com/queue" } }
              ]
            }
          }],
          "usageMetadata": { "promptTokenCount": 42, "candidatesTokenCount": 17 }
        }
        """;

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (GeminiAiService Service, FakeHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "chave-secreta",
            ["Gemini:Model"] = "modelo-teste",
            ["Gemini:BaseUrl"] = "https://fake.local",
        }).Build();
        return (new GeminiAiService(config, new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Parses_text_valid_unique_sources_and_usage()
    {
        var (service, _) = Create(HttpStatusCode.OK, GroundedResponse);

        AiAnswer answer = await service.AnswerAsync("O que é Outbox?", "Outbox", CancellationToken.None);

        Assert.Equal("Outbox grava o evento junto com o dado.\nDepois publica.", answer.Text);
        Assert.Equal(2, answer.Sources.Count);
        Assert.Equal(new AiSource("Transactional Outbox", "https://example.org/outbox"), answer.Sources[0]);
        Assert.Equal("http://example.com/queue", answer.Sources[1].Url);
        Assert.Equal(42, answer.InputTokens);
        Assert.Equal(17, answer.OutputTokens);
    }

    [Fact]
    public async Task Sends_key_in_header_not_in_url_and_requests_grounding()
    {
        var (service, handler) = Create(HttpStatusCode.OK, GroundedResponse);

        await service.AnswerAsync("Pergunta?", "", CancellationToken.None);

        Assert.Equal("https://fake.local/v1beta/models/modelo-teste:generateContent", handler.LastRequest!.RequestUri!.ToString());
        Assert.DoesNotContain("chave-secreta", handler.LastRequest.RequestUri.ToString());
        Assert.Equal("chave-secreta", handler.LastRequest.Headers.GetValues("x-goog-api-key").Single());
        Assert.NotNull(JsonNode.Parse(handler.LastBody!)!["tools"]![0]!["google_search"]);
    }

    [Fact]
    public async Task Response_without_grounding_has_no_sources()
    {
        var (service, _) = Create(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");

        AiAnswer answer = await service.AnswerAsync("?", "", CancellationToken.None);

        Assert.Empty(answer.Sources);
        Assert.Null(answer.InputTokens);
    }

    [Fact]
    public async Task Error_does_not_leak_response_body()
    {
        var (service, _) = Create(HttpStatusCode.BadRequest, """{"error":"prompt ecoado: segredo da nota"}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.AnswerAsync("?", "", CancellationToken.None));

        Assert.DoesNotContain("segredo", ex.Message);
    }

    [Fact]
    public async Task Fake_provider_has_no_sources()
    {
        IAiService fake = new FakeAiService();

        AiAnswer answer = await fake.AnswerAsync("?", "x", CancellationToken.None);

        Assert.Empty(answer.Sources);
    }

    [Fact]
    public void Source_entity_rejects_non_http_urls()
    {
        Assert.Throws<DomainException>(() => new ResearchSource(Guid.NewGuid(), Guid.NewGuid(), 0, "x", "ftp://example.org"));
    }
}
