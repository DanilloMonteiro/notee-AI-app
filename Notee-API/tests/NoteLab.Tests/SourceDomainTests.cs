using NoteLab.Domain;

namespace NoteLab.Tests;

public class SourceDomainTests
{
    [Theory]
    [InlineData(" https://doi.org/10.1234/ABC ", "10.1234/abc")]
    [InlineData("doi:10.5555/XYZ", "10.5555/xyz")]
    public void Doi_is_normalized(string input, string expected) =>
        Assert.Equal(expected, Source.NormalizeDoi(input));

    [Theory]
    [InlineData("10.12/x")]
    [InlineData("10.1234/")]
    [InlineData("10.1234567890/x")]
    public void Invalid_doi_is_rejected(string input) =>
        Assert.Throws<DomainException>(() => Source.NormalizeDoi(input));

    [Theory]
    [InlineData("0-306-40615-2", "9780306406157")]
    [InlineData("978-0-306-40615-7", "9780306406157")]
    public void Valid_isbn_is_normalized_to_isbn13(string input, string expected) =>
        Assert.Equal(expected, Source.NormalizeIsbn(input));

    [Theory]
    [InlineData("0306406153")]
    [InlineData("9780306406158")]
    [InlineData("978030640615X")]
    public void Invalid_isbn_is_rejected(string input) =>
        Assert.Throws<DomainException>(() => Source.NormalizeIsbn(input));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@example.org/book")]
    public void Source_rejects_unsafe_urls(string url) =>
        Assert.Throws<DomainException>(() => new Source("owner", SourceKind.Book, "Livro", [], null,
            null, null, null, null, url, null, null));

    [Fact]
    public void Citation_requires_anchor_fields_to_be_consistent()
    {
        Assert.Throws<DomainException>(() => new SourceCitation("owner", Guid.NewGuid(), Guid.NewGuid(),
            "block-1", "texto", 2, 99, SourceLocatorKind.None, null, null, null, null, null, null));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(9, 8)]
    public void Citation_rejects_invalid_page_range(int pageStart, int? pageEnd)
    {
        Assert.Throws<DomainException>(() => new SourceCitation("owner", Guid.NewGuid(), Guid.NewGuid(),
            null, null, null, null, SourceLocatorKind.Pages, null, pageStart, pageEnd, null, null, null));
    }

    [Fact]
    public void Citation_accepts_page_and_timestamp_locators()
    {
        var page = new SourceCitation("owner", Guid.NewGuid(), Guid.NewGuid(), null, null, null, null,
            SourceLocatorKind.Pages, null, 12, 14, null, null, null);
        var video = new SourceCitation("owner", Guid.NewGuid(), Guid.NewGuid(), null, null, null, null,
            SourceLocatorKind.Timestamp, null, null, null, 754, 790, "trecho literal");

        Assert.Equal(12, page.PageStart);
        Assert.Equal(14, page.PageEnd);
        Assert.Equal(754, video.TimeStartSec);
        Assert.Equal(790, video.TimeEndSec);
    }
}
