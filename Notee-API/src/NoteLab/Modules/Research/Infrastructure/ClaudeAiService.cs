using Anthropic;
using Anthropic.Models.Messages;
using NoteLab.Application;

namespace NoteLab.Infrastructure;

/// <summary>
/// Adaptador Anthropic Claude (SDK oficial). Usa a ferramenta de busca na web do servidor para devolver
/// fontes reais (URLs vindas dos resultados de busca). Modelo padrão: claude-opus-5-5 (configurável
/// por Claude:Model ou pelo painel). Usado SOMENTE com consentimento da conta (ADR-019).
/// </summary>
public sealed class ClaudeAiService : IAiService
{
    private readonly AiSettings _settings;
    private readonly AiProviderInfo _provider = AiProviders.Find(AiProviders.Claude)!;

    public ClaudeAiService(AiSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> GetAnswerAsync(string question, string exactQuote, CancellationToken ct) =>
        (await AnswerAsync(question, exactQuote, ct)).Text;

    public async Task<AiAnswer> AnswerAsync(string question, string exactQuote, CancellationToken ct)
    {
        string apiKey = _settings.ApiKeyFor(_provider) is { } key && AiProviders.IsRealKey(key)
            ? key
            : throw new InvalidOperationException("Claude sem chave configurada.");

        AnthropicClient client = new() { ApiKey = apiKey, BaseUrl = _settings.BaseUrlFor(_provider) };

        Message message = await client.Messages.Create(new MessageCreateParams
        {
            Model = _settings.ModelFor(_provider),
            MaxTokens = 16000,
            System = AiPrompts.System,
            Tools = [new ToolUnion(new WebSearchTool20260209 { MaxUses = 3 })],
            Messages = [new UserMessageParam { Content = AiPrompts.User(question, exactQuote) }],
        }, cancellationToken: ct);

        return ToAnswer(message);
    }

    /// <summary>Texto final + fontes dos resultados de busca (somente http/https, sem duplicatas).</summary>
    public static AiAnswer ToAnswer(Message message)
    {
        if (message.StopReason == StopReason.Refusal)
        {
            // Recusa do modelo: tratada como falha do job (sem texto inventado).
            throw new InvalidOperationException("Claude recusou a solicitação.");
        }

        var text = new List<string>();
        var sources = new List<AiSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ContentBlock block in message.Content)
        {
            if (block.TryPickText(out TextBlock? t))
            {
                text.Add(t.Text);
            }
            else if (block.TryPickWebSearchToolResult(out WebSearchToolResultBlock? result)
                     && result.Content.TryPickWebSearchResultBlocks(out IReadOnlyList<WebSearchResultBlock>? results))
            {
                foreach (WebSearchResultBlock r in results)
                {
                    if (sources.Count < GeminiAiService.MaxSources
                        && Uri.TryCreate(r.Url, UriKind.Absolute, out Uri? uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        && seen.Add(uri.ToString()))
                    {
                        sources.Add(new AiSource(string.IsNullOrWhiteSpace(r.Title) ? uri.Host : r.Title, uri.ToString()));
                    }
                }
            }
        }

        string answer = string.Join("", text).Trim();
        return new AiAnswer(answer.Length == 0 ? "Não foi possível gerar uma resposta." : answer, sources,
            (int?)message.Usage.InputTokens, (int?)message.Usage.OutputTokens);
    }
}
