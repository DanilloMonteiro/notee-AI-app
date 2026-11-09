using NoteLab.Domain;

namespace NoteLab.Tests;

public class AnalysisTests
{
    [Theory]
    [InlineData(true, true, AutoResearchOverride.Inherit, true)]
    [InlineData(true, false, AutoResearchOverride.Inherit, false)]
    [InlineData(true, false, AutoResearchOverride.On, true)]
    [InlineData(true, true, AutoResearchOverride.Off, false)]
    [InlineData(false, true, AutoResearchOverride.On, false)]
    public void Auto_research_precedence(bool researchEnabled, bool accountAuto, AutoResearchOverride note, bool expected)
    {
        var prefs = UserPreferences.Default("u");
        prefs.SetResearchEnabled(researchEnabled);
        prefs.SetAutoResearch(accountAuto);

        Assert.Equal(expected, NoteSettings.ShouldAutoResearch(prefs, note));
    }

    [Fact]
    public void Suggestion_is_decided_only_once()
    {
        var note = new Note(new NoteTitle("x"), "u", "y");
        var suggestion = new NoteSuggestion(note, NoteSuggestionKind.Tag, "política", "motivo", "fake");

        suggestion.Decide(NoteSuggestionStatus.Accepted);

        Assert.Throws<DomainException>(() => suggestion.Decide(NoteSuggestionStatus.Dismissed));
        Assert.Equal(1, suggestion.NoteVersion);
    }

    [Fact]
    public void Suggestion_requires_value()
    {
        var note = new Note(new NoteTitle("x"), "u", "y");
        Assert.Throws<DomainException>(() => new NoteSuggestion(note, NoteSuggestionKind.Doubt, "  ", "r", "fake"));
    }
}
