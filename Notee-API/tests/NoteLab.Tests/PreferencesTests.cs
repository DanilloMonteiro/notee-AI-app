using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

public class UserPreferencesTests
{
    [Fact]
    public void Default_has_research_on_and_no_external_consent()
    {
        var prefs = UserPreferences.Default("u1");

        Assert.True(prefs.ResearchEnabled);
        Assert.False(prefs.ExternalAiConsent);
        Assert.False(prefs.AllowsExternalAi);
    }

    [Fact]
    public void Consent_requires_current_version()
    {
        var prefs = UserPreferences.Default("u1");

        Assert.Throws<DomainException>(() => prefs.SetExternalAiConsent(true, null));
        Assert.Throws<DomainException>(() => prefs.SetExternalAiConsent(true, UserPreferences.CurrentConsentVersion + 1));

        prefs.SetExternalAiConsent(true, UserPreferences.CurrentConsentVersion);
        Assert.True(prefs.AllowsExternalAi);
        Assert.NotNull(prefs.ConsentedAt);
    }

    [Fact]
    public void Revoking_consent_blocks_external_immediately()
    {
        var prefs = UserPreferences.Default("u1");
        prefs.SetExternalAiConsent(true, UserPreferences.CurrentConsentVersion);

        prefs.SetExternalAiConsent(false, null);

        Assert.False(prefs.AllowsExternalAi);
        Assert.Null(prefs.ConsentedAt);
    }

    [Fact]
    public void Disabling_research_blocks_external_even_with_consent()
    {
        var prefs = UserPreferences.Default("u1");
        prefs.SetExternalAiConsent(true, UserPreferences.CurrentConsentVersion);

        prefs.SetResearchEnabled(false);

        Assert.False(prefs.AllowsExternalAi);
    }

    [Fact]
    public void Each_change_increments_version()
    {
        var prefs = UserPreferences.Default("u1");
        prefs.SetResearchEnabled(false);
        prefs.SetExternalAiConsent(false, null);

        Assert.Equal(3, prefs.Version);
    }
}

public class FakeAiServiceTests
{
    [Fact]
    public async Task Fake_is_deterministic_and_declares_no_source()
    {
        var fake = new FakeAiService();

        string a = await fake.GetAnswerAsync("O que é X?", "trecho", CancellationToken.None);
        string b = await fake.GetAnswerAsync("O que é X?", "trecho", CancellationToken.None);

        Assert.Equal(a, b);
        Assert.StartsWith(FakeAiService.Marker, a);
        Assert.Contains("Sem fonte", a);
    }
}
