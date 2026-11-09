using NoteLab.Domain;
using NoteLab.Infrastructure;

namespace NoteLab.Application;

/// <summary>
/// Decide qual provider responde a uma dúvida (ADR-019):
/// externo (Gemini) somente se o dono consentiu e há chave configurada; caso contrário, Fake determinístico.
/// </summary>
public sealed class AiProviderSelector
{
    private readonly FakeAiService _fake;
    private readonly IServiceProvider _services;

    public AiProviderSelector(FakeAiService fake, IServiceProvider services, IConfiguration config)
    {
        _fake = fake;
        _services = services;
        ExternalConfigured = IsRealKey(config["Gemini:ApiKey"]);
    }

    public bool ExternalConfigured { get; }

    public IAiService For(UserPreferences prefs) =>
        prefs.AllowsExternalAi && ExternalConfigured
            ? _services.GetRequiredService<GeminiAiService>()
            : _fake;

    public string Describe(UserPreferences prefs) =>
        prefs.AllowsExternalAi && ExternalConfigured ? "gemini" : "fake";

    private static bool IsRealKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && !key.StartsWith("COLOQUE", StringComparison.OrdinalIgnoreCase)
        && !key.Contains("SUA_CHAVE", StringComparison.OrdinalIgnoreCase);
}
