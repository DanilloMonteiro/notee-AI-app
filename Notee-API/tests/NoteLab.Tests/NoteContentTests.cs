using System.Text.Json;
using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

public class NoteContentTests
{
    private static Dictionary<string, object?> Chart(
        string chartType = "line",
        object?[]? labels = null,
        object?[]? series = null,
        object?[]? sources = null)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = "c1",
            ["type"] = "chart",
            ["schemaVersion"] = 1,
            ["chartType"] = chartType,
            ["title"] = "Inflação anual",
            ["description"] = "IPCA por ano",
            ["xAxisLabel"] = "Ano",
            ["yAxisLabel"] = "Variação",
            ["unit"] = "%",
            ["labels"] = labels ?? new object?[] { "2019", "2020", "2021" },
            ["series"] = series ?? new object?[]
            {
                new Dictionary<string, object?> { ["name"] = "IPCA", ["values"] = new object?[] { 4.31, null, 10.06 } },
            },
            ["sources"] = sources ?? new object?[]
            {
                new Dictionary<string, object?> { ["label"] = "IBGE", ["url"] = "https://www.ibge.gov.br" },
            },
        };
    }

    private static string Content(params object[] blocks) => JsonSerializer.Serialize(blocks);

    private static readonly Dictionary<string, object?> Paragraph = new() { ["id"] = "b1", ["type"] = "paragraph", ["text"] = "Olá" };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("texto antigo")]
    [InlineData("[texto solto")]
    [InlineData("{\"a\":1}")]
    [InlineData("[]")]
    [InlineData("[{\"id\":\"b1\",\"type\":\"paragraph\",\"text\":\"x\"}]")]
    public void Accepts_legacy_and_text_content(string? content)
    {
        NoteContent.Validate(content);
    }

    [Theory]
    [InlineData("line")]
    [InlineData("bar")]
    public void Accepts_valid_chart_with_missing_values(string chartType)
    {
        NoteContent.Validate(Content(Paragraph, Chart(chartType)));
    }

    [Fact]
    public void Accepts_chart_without_sources()
    {
        Dictionary<string, object?> chart = Chart();
        chart.Remove("sources");
        NoteContent.Validate(Content(chart));
    }

    [Fact]
    public void Rejects_unsupported_chart_type()
    {
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart("pie"))));
    }

    [Fact]
    public void Rejects_unsupported_schema_version()
    {
        Dictionary<string, object?> chart = Chart();
        chart["schemaVersion"] = 2;
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(chart)));
    }

    [Fact]
    public void Rejects_misaligned_series()
    {
        object?[] series = [new Dictionary<string, object?> { ["name"] = "IPCA", ["values"] = new object?[] { 1, 2 } }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(series: series))));
    }

    [Fact]
    public void Rejects_non_numeric_values()
    {
        object?[] series = [new Dictionary<string, object?> { ["name"] = "IPCA", ["values"] = new object?[] { 1, "2", 3 } }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(series: series))));
    }

    [Fact]
    public void Rejects_number_outside_double_range()
    {
        string content = "[{\"type\":\"chart\",\"chartType\":\"bar\",\"labels\":[\"a\"],\"series\":[{\"name\":\"s\",\"values\":[1e400]}]}]";
        Assert.Throws<DomainException>(() => NoteContent.Validate(content));
    }

    [Fact]
    public void Rejects_empty_labels_and_series()
    {
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(labels: []))));
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(series: []))));
    }

    [Fact]
    public void Rejects_series_without_name()
    {
        object?[] series = [new Dictionary<string, object?> { ["name"] = " ", ["values"] = new object?[] { 1, 2, 3 } }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(series: series))));
    }

    [Fact]
    public void Rejects_content_above_limits()
    {
        object?[] manyLabels = Enumerable.Range(0, NoteContent.MaxLabels + 1).Select(i => (object?)i.ToString()).ToArray();
        object?[] series = [new Dictionary<string, object?> { ["name"] = "s", ["values"] = manyLabels.Select(_ => (object?)1).ToArray() }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(labels: manyLabels, series: series))));

        object?[] manySeries = Enumerable.Range(0, NoteContent.MaxSeries + 1)
            .Select(i => (object?)new Dictionary<string, object?> { ["name"] = $"s{i}", ["values"] = new object?[] { 1, 2, 3 } })
            .ToArray();
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(series: manySeries))));

        Dictionary<string, object?> longTitle = Chart();
        longTitle["title"] = new string('a', NoteContent.MaxTitleLength + 1);
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(longTitle)));

        object?[] manySources = Enumerable.Range(0, NoteContent.MaxSources + 1)
            .Select(i => (object?)new Dictionary<string, object?> { ["label"] = $"f{i}" })
            .ToArray();
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(sources: manySources))));
    }

    [Fact]
    public void Rejects_more_than_max_charts()
    {
        object[] charts = Enumerable.Range(0, NoteContent.MaxChartsPerNote + 1).Select(_ => (object)Chart()).ToArray();
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(charts)));

        object[] allowed = Enumerable.Range(0, NoteContent.MaxChartsPerNote).Select(_ => (object)Chart()).ToArray();
        NoteContent.Validate(Content(allowed));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://exemplo.com/dados")]
    [InlineData("/relativa")]
    public void Rejects_unsafe_source_urls(string url)
    {
        object?[] sources = [new Dictionary<string, object?> { ["label"] = "Fonte", ["url"] = url }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(sources: sources))));
    }

    [Fact]
    public void Rejects_source_without_label()
    {
        object?[] sources = [new Dictionary<string, object?> { ["label"] = "", ["url"] = "https://a.b" }];
        Assert.Throws<DomainException>(() => NoteContent.Validate(Content(Chart(sources: sources))));
    }

    [Fact]
    public void Plain_text_includes_chart_metadata_but_not_values()
    {
        string text = NoteText.Extract(Content(Paragraph, Chart()), 1000);

        Assert.StartsWith("Olá\n", text);
        Assert.Contains("Inflação anual", text);
        Assert.Contains("IPCA por ano", text);
        Assert.Contains("2020", text);
        Assert.Contains("IBGE", text);
        Assert.DoesNotContain("10.06", text);
    }
}
