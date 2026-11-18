namespace NoteLab.Application;

/// <summary>De onde veio uma fonte encontrada (ADR-047). Novos tipos (livros, governo…) entram aqui.</summary>
public enum SourceOrigin
{
    Web,
    Academic,
}

/// <summary>
/// Quais origens consultar. <see cref="Mixed"/>: artigos primeiro; a internet só completa quando faltam artigos.
/// </summary>
public enum SourceMode
{
    Mixed,
    AcademicOnly,
    WebOnly,
    None,
}

public static class SourceModes
{
    public const string Mixed = "mixed";
    public const string Academic = "academic";
    public const string Web = "web";
    public const string None = "none";

    /// <summary>Valor do contrato (mixed|academic|web|none); null quando não reconhecido.</summary>
    public static SourceMode? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Mixed => SourceMode.Mixed,
        Academic => SourceMode.AcademicOnly,
        Web => SourceMode.WebOnly,
        None => SourceMode.None,
        _ => null,
    };

    public static string ToWire(SourceMode mode) => mode switch
    {
        SourceMode.AcademicOnly => Academic,
        SourceMode.WebOnly => Web,
        SourceMode.None => None,
        _ => Mixed,
    };

    public static bool IncludesAcademic(SourceMode mode) => mode is SourceMode.Mixed or SourceMode.AcademicOnly;

    public static bool IncludesWeb(SourceMode mode) => mode is SourceMode.Mixed or SourceMode.WebOnly;
}

/// <summary>
/// Um resultado de qualquer origem, no formato único que a IA lê e o Notee grava.
/// <see cref="SiteName"/>: site (web) ou periódico (artigo). <see cref="Score"/>: relevância de 0 a 1 dentro da origem.
/// <see cref="Authority"/>: -1 (desconhecida) a 4, só para a web.
/// </summary>
public sealed record SourceHit(
    string Title,
    string Url,
    string Excerpt,
    string? SiteName,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<string> Authors,
    string? Doi,
    SourceOrigin Origin,
    string Provider,
    double Score,
    int Authority = -1,
    bool Trusted = false)
{
    public string OriginWire => Origin == SourceOrigin.Academic ? "academic" : "web";
}

/// <summary>
/// Consulta já preparada: palavras-chave em português e em inglês (bases acadêmicas acham mais em inglês)
/// e a pergunta original (até 128 caracteres) para o buscador entender a intenção.
/// </summary>
public sealed record SourceQuery(string Pt, string En, string Intent);

/// <summary>Falha de uma origem (sem saldo, chave inválida, limite, indisponível). Quem chama segue sem ela.</summary>
public sealed class SourceSearchException(string message, int? status = null) : Exception(message)
{
    public int? Status { get; } = status;
}

/// <summary>Uma origem de fontes plugável (DeepSeek Search, OpenAlex…). Só a consulta sai do Notee.</summary>
public interface ISourceSearchProvider
{
    public string Name { get; }

    public SourceOrigin Origin { get; }

    /// <summary>Configurado e ligado no servidor.</summary>
    public bool Available { get; }

    public Task<IReadOnlyList<SourceHit>> SearchAsync(SourceQuery query, CancellationToken ct);
}

/// <summary>
/// Sites bloqueados e confiáveis de uma pessoa, já somados aos bloqueios fixos do servidor (ex.: Wikipédia).
/// Um domínio vale para ele e para os subdomínios (wikipedia.org cobre pt.wikipedia.org).
/// </summary>
public sealed class SourceRules
{
    public static readonly SourceRules Empty = new([], []);

    private readonly HashSet<string> _blocked;
    private readonly HashSet<string> _trusted;

    public SourceRules(IEnumerable<string> blocked, IEnumerable<string> trusted)
    {
        _blocked = new HashSet<string>(blocked.Select(NormalizeDomain).OfType<string>(), StringComparer.OrdinalIgnoreCase);
        _trusted = new HashSet<string>(trusted.Select(NormalizeDomain).OfType<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Blocked => _blocked;

    public IReadOnlyCollection<string> Trusted => _trusted;

    public bool IsBlocked(string? url) => Matches(_blocked, url);

    public bool IsTrusted(string? url) => Matches(_trusted, url);

    /// <summary>Domínio "limpo": sem esquema, caminho, porta e "www."; null se não for um domínio válido.</summary>
    public static string? NormalizeDomain(string? value)
    {
        string text = (value ?? "").Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || uri.HostNameType != UriHostNameType.Dns || !uri.Host.Contains('.'))
        {
            return null;
        }

        string host = uri.Host.TrimEnd('.');
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static bool Matches(HashSet<string> domains, string? url)
    {
        if (domains.Count == 0 || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        foreach (string domain in domains)
        {
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Configuração <c>Research:Academic</c> (OpenAlex) e <c>Research:Sources</c>.</summary>
public sealed record SourceSearchOptions(
    bool AcademicEnabled,
    double AcademicMinScore,
    int AcademicMaxPerDay,
    decimal AcademicPricePer1000Usd,
    int MinAcademicForArticlesOnly,
    int MaxResults,
    IReadOnlyList<string> BlockedDomains)
{
    /// <summary>Bloqueados sempre (a pessoa não remove): enciclopédias abertas editáveis por qualquer um.</summary>
    public static readonly string[] DefaultBlocked = ["wikipedia.org", "wikimedia.org", "wikiwand.com"];

    public static SourceSearchOptions From(IConfiguration config)
    {
        string[] blocked = config.GetSection("Research:Sources:BlockedDomains").Get<string[]>() ?? [];
        return new(
            config.GetValue<bool?>("Research:Academic:Enabled") ?? true,
            Math.Clamp(config.GetValue<double?>("Research:Academic:MinScore") ?? 0.35, 0, 1),
            Math.Max(0, config.GetValue<int?>("Research:Academic:MaxPerDay") ?? 900),
            config.GetValue<decimal?>("Research:Academic:PricePer1000Usd") ?? 1m,
            Math.Clamp(config.GetValue<int?>("Research:Sources:MinAcademicForArticlesOnly") ?? 3, 1, 10),
            Math.Clamp(config.GetValue<int?>("Research:Sources:MaxResults") ?? 6, 1, 10),
            DefaultBlocked.Concat(blocked).Select(SourceRules.NormalizeDomain).OfType<string>().Distinct().ToList());
    }

    /// <summary>Custo de N consultas ao OpenAlex (erros não são cobrados).</summary>
    public decimal AcademicCostOf(int calls) => Math.Round(calls * AcademicPricePer1000Usd / 1000m, 6);
}

/// <summary>
/// Regras puras de seleção (ADR-047): tira bloqueados e itens sem resumo, marca confiáveis,
/// aplica a nota mínima (confiáveis com nota menor), remove repetidos (DOI ou site+caminho),
/// limita por site e ordena: confiáveis primeiro, depois a nota.
/// </summary>
public static class SourceMix
{
    public static List<SourceHit> Select(IEnumerable<SourceHit> hits, double minScore, int maxPerSite, int take, SourceRules rules,
        ISet<string>? seen = null)
    {
        seen ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var picked = new List<SourceHit>();
        var perSite = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<SourceHit> candidates = hits
            .Where(h => !rules.IsBlocked(h.Url) && !string.IsNullOrWhiteSpace(h.Excerpt))
            .Select(h => rules.IsTrusted(h.Url) ? h with { Trusted = true } : h)
            .Where(h => h.Score >= (h.Trusted ? minScore / 2 : minScore))
            .OrderByDescending(h => h.Trusted)
            .ThenByDescending(h => h.Score);

        foreach (SourceHit hit in candidates)
        {
            if (picked.Count >= take)
            {
                break;
            }

            bool parsed = Uri.TryCreate(hit.Url, UriKind.Absolute, out Uri? uri);
            string site = parsed ? SourceRules.NormalizeDomain(uri!.Host) ?? uri.Host : hit.Url;
            // Mesmo artigo (DOI) ou mesma página com e sem "www."/barra final conta uma vez só.
            string key = hit.Doi is { Length: > 0 } doi ? $"doi:{doi}" : parsed ? $"{site}{uri!.PathAndQuery.TrimEnd('/')}" : hit.Url;
            if (!seen.Add(key) || perSite.GetValueOrDefault(site) >= maxPerSite)
            {
                continue;
            }

            perSite[site] = perSite.GetValueOrDefault(site) + 1;
            picked.Add(hit);
        }

        return picked;
    }

    /// <summary>Quantos por cento das fontes vieram de artigos (0–100); null sem fontes.</summary>
    public static int? AcademicPercent(IEnumerable<SourceOrigin> origins)
    {
        var list = origins.ToList();
        return list.Count == 0 ? null : (int)Math.Round(100.0 * list.Count(o => o == SourceOrigin.Academic) / list.Count);
    }
}
