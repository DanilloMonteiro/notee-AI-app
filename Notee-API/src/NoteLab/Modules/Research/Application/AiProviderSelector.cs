using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

/// <summary>
/// Decide qual provider responde a uma dúvida (ADR-019/030):
/// o provedor externo ATIVO (escolhido pelo administrador) só é usado se a conta consentiu
/// e se esse provedor tem chave configurada; caso contrário, Fake determinístico.
/// </summary>
public sealed class AiProviderSelector
{
    private readonly FakeAiService _fake;
    private readonly IServiceProvider _services;
    private readonly AiSettings _settings;

    public AiProviderSelector(FakeAiService fake, IServiceProvider services, AiSettings settings)
    {
        _fake = fake;
        _services = services;
        _settings = settings;
    }

    public AiProviderInfo Active => AiProviders.Find(_settings.ActiveProvider) ?? AiProviders.All[0];

    /// <summary>O provedor ativo tem chave configurada?</summary>
    public bool ExternalConfigured => _settings.IsConfigured(Active);

    public IAiService For(UserPreferences prefs) =>
        prefs.AllowsExternalAi && ExternalConfigured ? Resolve(Active.Id) : _fake;

    public string Describe(UserPreferences prefs) =>
        prefs.AllowsExternalAi && ExternalConfigured ? Active.Id : "fake";

    /// <summary>Instância do adaptador de um provedor específico (usado também no teste do painel admin).</summary>
    public IAiService Resolve(string providerId) => providerId switch
    {
        AiProviders.Gemini => _services.GetRequiredService<GeminiAiService>(),
        AiProviders.ChatGpt => _services.GetRequiredService<ChatGptAiService>(),
        AiProviders.Claude => _services.GetRequiredService<ClaudeAiService>(),
        AiProviders.Grok => _services.GetRequiredService<GrokAiService>(),
        AiProviders.DeepSeek => _services.GetRequiredService<DeepSeekAiService>(),
        _ => throw new ArgumentException($"Provedor desconhecido: {providerId}"),
    };
}
