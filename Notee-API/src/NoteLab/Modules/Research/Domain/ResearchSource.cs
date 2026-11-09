namespace NoteLab.Domain;

/// <summary>
/// Fonte devolvida pelo provider para a resposta de uma dúvida (M13). Registro separado da resposta:
/// guarda só título e URL que a API retornou — nada é inferido ou inventado.
/// </summary>
public sealed class ResearchSource
{
    public Guid Id { get; private set; }
    public Guid DoubtId { get; private set; }
    public Guid JobId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; }
    public string Url { get; private set; }
    public DateTimeOffset RetrievedAt { get; private set; }

    private ResearchSource()
    {
        Title = null!;
        Url = null!;
    }

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
        Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title.Trim();
        Url = uri.ToString();
        RetrievedAt = DateTimeOffset.UtcNow;
    }
}
