using Microsoft.Extensions.Configuration;
using System.Net;
using NoteLab.Application;
using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

public sealed class SourceVerificationTests
{
    [Fact]
    public void MetadataComparisonReportsYearAndTitleDifferences()
    {
        var result = SourceMetadataComparer.Compare("IPCA anual", 2017, "Índice mensal", 2016, "https://crossref.test/item");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Point == "Ano divergente");
        Assert.Contains(result, item => item.Point == "Título divergente");
    }

    [Fact]
    public void MetadataComparisonIgnoresAccentsAndPunctuation()
    {
        Assert.Empty(SourceMetadataComparer.Compare("Índice — anual", 2017, "Indice anual", 2017, "https://example.test"));
    }

    [Fact]
    public void MetadataComparisonReportsDifferentFirstAuthor()
    {
        List<SourceCheckArgument> result = SourceMetadataComparer.Compare("Estudo", 2020, "Estudo", 2020,
            "https://crossref.test/item", ["Ana Silva"], ["Maria Souza"]);
        Assert.Contains(result, item => item.Point == "Autor divergente");
    }

    [Fact]
    public void QuoteMatcherFindsQuoteOnRequestedPage()
    {
        SourceCheckResult result = SourceQuoteMatcher.Compare(
            [new(1, "Texto inicial."), new(2, "A economia cresceu durante o ano.")],
            "A economia cresceu durante o ano", 2, 2);
        Assert.Equal(SourceCheckStatus.Confirmed, result.Status);
    }

    [Fact]
    public void QuoteMatcherReportsQuoteFoundOnAnotherPage()
    {
        SourceCheckResult result = SourceQuoteMatcher.Compare(
            [new(1, "Texto inicial."), new(2, "A economia cresceu durante o ano.")],
            "A economia cresceu durante o ano", 1, 1);
        Assert.Equal(SourceCheckStatus.FoundWithDifferences, result.Status);
        Assert.Contains("página 2", result.Summary);
    }

    [Fact]
    public void QuoteMatcherDistinguishesMissingQuote()
    {
        SourceCheckResult result = SourceQuoteMatcher.Compare([new(1, "Outro conteúdo")], "Um trecho ausente");
        Assert.Equal(SourceCheckStatus.QuoteNotFound, result.Status);
    }

    [Fact]
    public void AiResponseRequiresLiteralEvidenceFromSubmittedExcerpt()
    {
        const string excerpt = "O indicador subiu 4,2 por cento no período.";
        const string valid = "{\"verdict\":\"supports\",\"arguments\":[{\"point\":\"O trecho menciona a alta\",\"sourceExcerpt\":\"subiu 4,2 por cento\"}]}";
        Assert.Equal("supports", SourceAiResponseValidator.Parse(valid, excerpt).Verdict);

        const string invalid = "{\"verdict\":\"supports\",\"arguments\":[{\"point\":\"Evidência inventada\",\"sourceExcerpt\":\"cresceu 20 por cento\"}]}";
        Assert.Throws<InvalidAiSourceCheckException>(() => SourceAiResponseValidator.Parse(invalid, excerpt));
    }

    [Theory]
    [InlineData("<link rel=\"license\" href=\"https://creativecommons.org/licenses/by-sa/4.0/\">", "cc-by-sa")]
    [InlineData("<link href='https://creativecommons.org/licenses/by-nc/4.0/' rel='alternate license'>", "cc-by-nc")]
    [InlineData("<link rel=\"license\" href=\"https://creativecommons.org/publicdomain/zero/1.0/\">", "cc-zero")]
    public void CreativeCommonsLicenseRequiresRecognizedCreativeCommonsPath(string tag, string expected) =>
        Assert.Equal(expected, SourcePageMetadata.CreativeCommonsLicense(tag));

    [Theory]
    [InlineData("cc0", "cc-zero")]
    [InlineData("cc-by-nc-sa", "cc-by-nc-sa")]
    [InlineData("cc-proprietary", null)]
    [InlineData("open-access", null)]
    public void CopyingRequiresARecognizedLicense(string input, string? expected) =>
        Assert.Equal(expected, SourcePageMetadata.NormalizeCopyLicense(input));

    [Theory]
    [InlineData("<link rel=\"license\" href=\"https://creativecommons.org.evil.test/licenses/by/4.0/\">")]
    [InlineData("<link rel=\"license\" href=\"https://example.org/licenses/by/4.0/\">")]
    [InlineData("<link rel=\"alternate\" href=\"https://creativecommons.org/licenses/by/4.0/\">")]
    public void CreativeCommonsLicenseRejectsUntrustedOrMissingLicense(string html) =>
        Assert.Null(SourcePageMetadata.CreativeCommonsLicense(html));

    [Fact]
    public void HtmlExtractionDropsScriptAndMarkupAndDecodesEntities()
    {
        string text = SourcePageMetadata.ExtractVisibleText("<main><p>Dados &amp; contexto</p><script>segredo()</script><style>.x {}</style></main>");
        Assert.Contains("Dados & contexto", text);
        Assert.DoesNotContain("segredo", text);
        Assert.DoesNotContain("<p>", text);
    }

    [Fact]
    public async Task SafeHttpClientBlocksLoopbackAndNonHttpSchemes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        using var client = new SafeHttpClient(configuration);
        await Assert.ThrowsAsync<SafeHttpException>(() => client.GetAsync("http://127.0.0.1/private", CancellationToken.None));
        await Assert.ThrowsAsync<SafeHttpException>(() => client.GetAsync("file:///etc/passwd", CancellationToken.None));
    }

    [Fact]
    public async Task SafeHttpClientRevalidatesRedirectTargetsBeforeSending()
    {
        int requests = 0;
        using var client = new SafeHttpClient(new StubHandler((_, _) =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://127.0.0.1/private") } };
        }));

        await Assert.ThrowsAsync<SafeHttpException>(() => client.GetAsync("https://example.org/start", CancellationToken.None));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task SafeHttpClientRejectsOversizedResponseFromHeaders()
    {
        using var client = new SafeHttpClient(new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[SafeHttpClient.MaxBytes + 1])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            return response;
        }));

        await Assert.ThrowsAsync<SafeHttpException>(() => client.GetAsync("https://example.org/large.txt", CancellationToken.None));
    }

    [Fact]
    public void SourceCheckRetriesWithBackoffAndBecomesPending()
    {
        var check = new SourceCheck("owner", Guid.NewGuid(), SourceCheckLayer.Existence);
        check.Claim("worker-1", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
        check.Retry("temporary failure");
        Assert.Equal(SourceCheckStatus.Pending, check.Status);
        Assert.Equal(1, check.Attempts);
        Assert.True(check.NextAttemptAt > DateTimeOffset.UtcNow);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }
}
