namespace NoteLab.Domain;

/// <summary>
/// Fonte da resposta de uma dúvida (M13, ADR-045). Registro separado da resposta.
/// <list type="bullet">
/// <item><see cref="Verified"/> = true: URL devolvida pela busca do provedor, ou referência declarada que o Notee encontrou.</item>
/// <item><see cref="Verified"/> = false: origem só DECLARADA pela IA e não encontrada — guarda o rótulo, nunca um link.</item>
/// </list>
/// <see cref="Position"/> + 1 é o número usado nos marcadores [n] do texto da resposta.
/// </summary>
public sealed class ResearchSource
{
    public const int MaxTitleLength = 300;

    public Guid Id { get; private set; }
    public Guid DoubtId { get; private set; }
    public Guid JobId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; }
    public string? Url { get; private set; }
    public bool Verified { get; private set; }
    public DateTimeOffset RetrievedAt { get; private set; }

    private ResearchSource()
    {
        Title = null!;
    }

    /// <summary>Fonte conferida, com link http/https.</summary>
    public ResearchSource(Guid doubtId, Guid jobId, int position, string title, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("Fonte com URL inválida.");
        }

        Id = Guid.NewGuid();
        DoubtId = doubtId;
        JobId = jobId;
        Position = position;
        Title = Clip(string.IsNullOrWhiteSpace(title) ? uri.Host : title.Trim());
        Url = uri.ToString();
        Verified = true;
        RetrievedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Origem informada pela IA que o Notee NÃO conseguiu conferir: só o rótulo, sem link.</summary>
    public static ResearchSource Declared(Guid doubtId, Guid jobId, int position, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Origem declarada sem rótulo.");
        }

        return new ResearchSource
        {
            Id = Guid.NewGuid(),
            DoubtId = doubtId,
            JobId = jobId,
            Position = position,
            Title = Clip(label.Trim()),
            Url = null,
            Verified = false,
            RetrievedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string Clip(string s) => s.Length <= MaxTitleLength ? s : s[..MaxTitleLength];
}
