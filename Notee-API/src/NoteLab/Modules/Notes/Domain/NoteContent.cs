using System.Text.Json;

namespace NoteLab.Domain;

/// <summary>
/// Validação do conteúdo da nota (ADR-036). O conteúdo continua sendo texto puro (notas antigas)
/// ou um array JSON de blocos. Somente blocos <c>type = "chart"</c> têm schema estrito;
/// os demais blocos mantêm o comportamento anterior.
/// </summary>
public static class NoteContent
{
    public const string ChartType = "chart";
    public const int ChartSchemaVersion = 1;
    public const int MaxChartsPerNote = 20;
    public const int MaxSeries = 8;
    public const int MaxLabels = 120;
    public const int MaxSources = 10;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxShortTextLength = 80;
    public const int MaxSourceLabelLength = 200;
    public const int MaxUrlLength = 2048;

    private static readonly string[] SupportedChartTypes = ["line", "bar"];

    public static void Validate(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith('['))
        {
            return;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            // Texto puro que começa com "[" continua aceito como antes.
            return;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int charts = 0;
            foreach (JsonElement block in doc.RootElement.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("type", out JsonElement type)
                    || type.ValueKind != JsonValueKind.String
                    || type.GetString() != ChartType)
                {
                    continue;
                }

                charts++;
                if (charts > MaxChartsPerNote)
                {
                    throw new DomainException($"Uma nota pode ter no máximo {MaxChartsPerNote} gráficos.");
                }

                ValidateChart(block, charts);
            }
        }
    }

    private static void ValidateChart(JsonElement chart, int position)
    {
        string where = $"Gráfico {position}";

        if (chart.TryGetProperty("schemaVersion", out JsonElement version)
            && (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int v) || v != ChartSchemaVersion))
        {
            throw new DomainException($"{where}: versão do formato não suportada.");
        }

        string chartType = RequiredString(chart, "chartType", where, MaxShortTextLength);
        if (!SupportedChartTypes.Contains(chartType))
        {
            throw new DomainException($"{where}: tipo de gráfico não suportado (use line ou bar).");
        }

        OptionalString(chart, "id", where, MaxShortTextLength);
        OptionalString(chart, "title", where, MaxTitleLength);
        OptionalString(chart, "description", where, MaxDescriptionLength);
        OptionalString(chart, "xAxisLabel", where, MaxShortTextLength);
        OptionalString(chart, "yAxisLabel", where, MaxShortTextLength);
        OptionalString(chart, "unit", where, MaxShortTextLength);

        JsonElement labels = RequiredArray(chart, "labels", where);
        int labelCount = labels.GetArrayLength();
        if (labelCount is < 1 or > MaxLabels)
        {
            throw new DomainException($"{where}: informe de 1 a {MaxLabels} rótulos.");
        }

        foreach (JsonElement label in labels.EnumerateArray())
        {
            if (label.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(label.GetString()))
            {
                throw new DomainException($"{where}: rótulos devem ser textos não vazios.");
            }

            if (label.GetString()!.Length > MaxShortTextLength)
            {
                throw new DomainException($"{where}: rótulo acima de {MaxShortTextLength} caracteres.");
            }
        }

        JsonElement series = RequiredArray(chart, "series", where);
        int seriesCount = series.GetArrayLength();
        if (seriesCount is < 1 or > MaxSeries)
        {
            throw new DomainException($"{where}: informe de 1 a {MaxSeries} séries.");
        }

        foreach (JsonElement serie in series.EnumerateArray())
        {
            if (serie.ValueKind != JsonValueKind.Object)
            {
                throw new DomainException($"{where}: série inválida.");
            }

            string name = RequiredString(serie, "name", where, MaxShortTextLength);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException($"{where}: toda série precisa de nome.");
            }

            JsonElement values = RequiredArray(serie, "values", where);
            if (values.GetArrayLength() != labelCount)
            {
                throw new DomainException($"{where}: a série \"{name}\" precisa ter um valor (ou null) para cada rótulo.");
            }

            foreach (JsonElement value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number))
                {
                    throw new DomainException($"{where}: a série \"{name}\" tem valor que não é número finito (use null para dado ausente).");
                }
            }
        }

        if (chart.TryGetProperty("sources", out JsonElement sources) && sources.ValueKind != JsonValueKind.Null)
        {
            if (sources.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException($"{where}: fontes devem ser uma lista.");
            }

            if (sources.GetArrayLength() > MaxSources)
            {
                throw new DomainException($"{where}: no máximo {MaxSources} fontes.");
            }

            foreach (JsonElement source in sources.EnumerateArray())
            {
                if (source.ValueKind != JsonValueKind.Object)
                {
                    throw new DomainException($"{where}: fonte inválida.");
                }

                string label = RequiredString(source, "label", where, MaxSourceLabelLength);
                if (string.IsNullOrWhiteSpace(label))
                {
                    throw new DomainException($"{where}: toda fonte precisa de um rótulo.");
                }

                string? url = OptionalString(source, "url", where, MaxUrlLength);
                if (!string.IsNullOrWhiteSpace(url) && !IsHttpUrl(url))
                {
                    throw new DomainException($"{where}: a URL da fonte \"{label}\" deve ser absoluta e usar http ou https.");
                }
            }
        }
    }

    public static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string RequiredString(JsonElement owner, string property, string where, int maxLength)
    {
        if (!owner.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new DomainException($"{where}: campo \"{property}\" obrigatório.");
        }

        string text = value.GetString()!;
        if (text.Length > maxLength)
        {
            throw new DomainException($"{where}: \"{property}\" acima de {maxLength} caracteres.");
        }

        return text;
    }

    private static string? OptionalString(JsonElement owner, string property, string where, int maxLength)
    {
        if (!owner.TryGetProperty(property, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new DomainException($"{where}: \"{property}\" deve ser texto.");
        }

        string text = value.GetString()!;
        if (text.Length > maxLength)
        {
            throw new DomainException($"{where}: \"{property}\" acima de {maxLength} caracteres.");
        }

        return text;
    }

    private static JsonElement RequiredArray(JsonElement owner, string property, string where)
    {
        if (!owner.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new DomainException($"{where}: campo \"{property}\" deve ser uma lista.");
        }

        return value;
    }
}
