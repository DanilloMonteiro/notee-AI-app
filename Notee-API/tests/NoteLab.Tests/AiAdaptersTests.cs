using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoteLab.Application;
using NoteLab.Infrastructure;

namespace NoteLab.Tests;

/// <summary>Adaptadores ChatGPT/Grok/DeepSeek com servidor HTTP falso (nenhuma chamada real).</summary>
public class AiAdaptersTests
{
    private const string Completion = """
        { "choices": [ { "message": { "role": "assistant", "content": "  ok  " } } ],
          "usage": { "prompt_tokens": 12, "completion_tokens": 3 } }
        """;

    [Theory]
    [InlineData("chatgpt", "OpenAI", "https://api.openai.com/v1", "gpt-4o-mini")]
    [InlineData("grok", "Grok", "https://api.x.ai/v1", "grok-4")]
    [InlineData("deepseek", "DeepSeek", "https://api.deepseek.com", "deepseek-chat")]
    public void Catalog_defaults(string id, string section, string url, string model)
    {
        AiProviderInfo p = AiProviders.Find(id)!;
        Assert.Equal(section, p.ConfigSection);
        Assert.Equal(url, p.DefaultBaseUrl);
        Assert.Equal(model, p.DefaultModel);
    }

    [Fact]
    public void Parses_chat_completion_without_inventing_sources()
    {
        AiAnswer answer = OpenAiCompatibleService.Parse(JsonNode.Parse(Completion));

        Assert.Equal("ok", answer.Text);
        Assert.Empty(answer.Sources);
        Assert.Equal(12, answer.InputTokens);
        Assert.Equal(3, answer.OutputTokens);
    }

    [Fact]
    public void Placeholder_keys_are_not_configured()
    {
        Assert.False(AiProviders.IsRealKey("COLOQUE_SUA_CHAVE_OPENAI_AQUI"));
        Assert.False(AiProviders.IsRealKey(""));
        Assert.True(AiProviders.IsRealKey("sk-de-teste"));
    }

    private static AiSettings Settings(Dictionary<string, string?> values) =>
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new EphemeralDataProtectionProvider());

    [Fact]
    public async Task Config_key_takes_precedence_and_is_not_editable()
    {
        AiSettings settings = Settings(new() { ["OpenAI:ApiKey"] = "sk-config" });
        AiProviderInfo chatgpt = AiProviders.Find("chatgpt")!;

        Assert.Equal(KeySource.Config, settings.SourceOf(chatgpt));
        Assert.Equal("sk-config", settings.ApiKeyFor(chatgpt));
        await Assert.ThrowsAsync<InvalidOperationException>(() => settings.SetPanelKeyAsync(chatgpt, "sk-painel", "admin", CancellationToken.None));
    }

}
