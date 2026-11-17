using System.Text.Json;
using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

/// <summary>Lentes e crítica de raciocínio — fatia F1 (domínio + gerador Fake).</summary>
public class PerspectiveTests
{
    private static Note BlockNote(string title, params (string Id, string Type, string Text)[] blocks)
    {
        string content = JsonSerializer.Serialize(blocks.Select(b => new { id = b.Id, type = b.Type, text = b.Text }));
        var note = new Note(new NoteTitle(title), "owner", content);
        note.SetPlainText(string.Join('\n', blocks.Select(b => b.Text)));
        return note;
    }

    private static Note PoliticalNote() => BlockNote("Governo Dilma",
        ("b1", "paragraph", "O governo Dilma enfrentou crise econômica com inflação alta e queda do PIB."),
        ("b2", "paragraph", "A recessão foi causada por causa da política fiscal. Portanto o impeachment era inevitável."),
        ("b3", "paragraph", "Todos os economistas obviamente concordam com essa leitura."));

    private static Note MathNote() => BlockNote("Derivadas",
        ("m1", "heading", "Regra da cadeia"),
        ("m2", "paragraph", "A derivada de uma função composta usa a regra da cadeia. A derivada mede a taxa de variação e a inclinação da reta tangente."),
        ("m3", "paragraph", "Portanto a derivada de sen(x²) é 2x·cos(x²), e isso vale sempre para qualquer função."));

    [Fact]
    public void Fake_is_deterministic_for_the_same_note()
    {
        Note note = PoliticalNote();
        var first = PerspectiveRules.Generate(note).Suggestions;
        var second = PerspectiveRules.Generate(note).Suggestions;

        Assert.Equal(
            first.Select(s => (s.Kind, s.Category, s.Prompt, s.Anchor)),
            second.Select(s => (s.Kind, s.Category, s.Prompt, s.Anchor)));
    }

    [Fact]
    public void Fake_respects_limits_and_only_asks_questions()
    {
        var result = PerspectiveRules.Generate(PoliticalNote());

        Assert.False(result.InsufficientContext);
        Assert.InRange(result.Suggestions.Count(s => s.Kind == PerspectiveKind.Lens), 1, PerspectiveRules.MaxLenses);
        Assert.InRange(result.Suggestions.Count(s => s.Kind == PerspectiveKind.Critique), 1, PerspectiveRules.MaxCritiques);
        Assert.All(result.Suggestions, s =>
        {
            Assert.EndsWith("?", s.Prompt);
            Assert.Equal(PerspectiveStatus.Pending, s.Status);
            Assert.Equal(PerspectiveRules.Provider, s.Provider);
        });
    }

    [Fact]
    public void Political_note_gets_humanities_lenses_with_balanced_wording()
    {
        var lenses = PerspectiveRules.Generate(PoliticalNote()).Suggestions.Where(s => s.Kind == PerspectiveKind.Lens).ToList();

        Assert.Contains(lenses, l => l.Category == PerspectiveCategories.Economic);
        Assert.Contains(lenses, l => l.Category == PerspectiveCategories.Political);
        Assert.Contains("defensores e os críticos", lenses.Single(l => l.Category == PerspectiveCategories.Political).Prompt);
        Assert.All(lenses, l => Assert.Null(l.Anchor));
    }

    [Fact]
    public void Math_note_gets_math_lenses()
    {
        var categories = PerspectiveRules.Generate(MathNote()).Suggestions
            .Where(s => s.Kind == PerspectiveKind.Lens).Select(s => s.Category).ToList();

        Assert.Contains(PerspectiveCategories.Geometric, categories);
        Assert.Contains(PerspectiveCategories.Formal, categories);
        Assert.DoesNotContain(PerspectiveCategories.Economic, categories);
    }

    [Fact]
    public void Critique_anchor_points_to_exact_text_in_the_block()
    {
        Note note = PoliticalNote();
        var blocks = JsonSerializer.Deserialize<JsonElement[]>(note.Content!)!
            .ToDictionary(b => b.GetProperty("id").GetString()!, b => b.GetProperty("text").GetString()!);

        var critiques = PerspectiveRules.Generate(note).Suggestions.Where(s => s.Kind == PerspectiveKind.Critique).ToList();

        Assert.All(critiques, c =>
        {
            DoubtAnchor anchor = c.Anchor!;
            string text = blocks[anchor.BlockId];
            Assert.Equal(anchor.ExactQuote, text[anchor.StartIndex..anchor.EndIndex]);
        });
        var unsupported = critiques.Single(c => c.Category == PerspectiveCategories.UnsupportedClaim);
        Assert.Equal("b3", unsupported.Anchor!.BlockId);
        Assert.Equal("Todos os economistas obviamente concordam com essa leitura.", unsupported.Anchor.ExactQuote);
    }

    [Fact]
    public void Short_note_reports_insufficient_context()
    {
        var result = PerspectiveRules.Generate(BlockNote("Ideia", ("x", "paragraph", "Só um rascunho curto.")));

        Assert.True(result.InsufficientContext);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public void Legacy_plain_text_note_gets_lenses_but_no_unanchored_critiques()
    {
        var note = new Note(new NoteTitle("Economia"), "owner",
            "A inflação subiu porque os juros caíram. Portanto todos os preços sempre sobem quando o governo gasta demais no mercado.");

        var result = PerspectiveRules.Generate(note);

        Assert.NotEmpty(result.Suggestions);
        Assert.All(result.Suggestions, s => Assert.Equal(PerspectiveKind.Lens, s.Kind));
    }

    [Fact]
    public void Accepting_critique_creates_anchored_doubt()
    {
        Note note = PoliticalNote();
        var critique = PerspectiveRules.Generate(note).Suggestions.First(s => s.Kind == PerspectiveKind.Critique);

        Doubt doubt = critique.Accept(note.Version);

        Assert.Equal(PerspectiveStatus.Accepted, critique.Status);
        Assert.Equal(doubt.Id, critique.DoubtId);
        Assert.Equal(DoubtOrigin.Perspective, doubt.Origin);
        Assert.Equal(critique.Anchor, doubt.Anchor);
        Assert.Equal(critique.Prompt, doubt.Question);
        Assert.Equal(note.Id, doubt.NoteId);
        Assert.Equal(note.OwnerId, doubt.OwnerId);
    }

    [Fact]
    public void Accepting_lens_creates_unanchored_doubt()
    {
        Note note = PoliticalNote();
        var lens = PerspectiveRules.Generate(note).Suggestions.First(s => s.Kind == PerspectiveKind.Lens);

        Doubt doubt = lens.Accept(note.Version);

        Assert.Null(doubt.Anchor);
        Assert.Equal(DoubtOrigin.Perspective, doubt.Origin);
    }

    [Fact]
    public void Edited_note_makes_suggestion_stale_and_blocks_accept()
    {
        Note note = PoliticalNote();
        var suggestion = PerspectiveRules.Generate(note).Suggestions[0];

        note.RegisterUpdate();

        Assert.True(suggestion.IsStale(note.Version));
        Assert.Throws<StalePerspectiveException>(() => suggestion.Accept(note.Version));
        Assert.Equal(PerspectiveStatus.Pending, suggestion.Status);
    }

    [Fact]
    public void Dismiss_keeps_reason_and_cannot_be_decided_twice()
    {
        Note note = PoliticalNote();
        var suggestion = PerspectiveRules.Generate(note).Suggestions[0];

        suggestion.Dismiss("  não se aplica  ");

        Assert.Equal(PerspectiveStatus.Dismissed, suggestion.Status);
        Assert.Equal("não se aplica", suggestion.DismissReason);
        Assert.Throws<DomainException>(() => suggestion.Accept(note.Version));
        Assert.Throws<DomainException>(() => suggestion.Dismiss(null));
    }

    [Fact]
    public void Domain_rejects_verdicts_and_inconsistent_anchors()
    {
        Note note = PoliticalNote();
        var anchor = new DoubtAnchor("b1", "governo", 2, 9);

        Assert.Throws<DomainException>(() =>
            new PerspectiveSuggestion(note, PerspectiveKind.Lens, PerspectiveCategories.Political, "O governo errou.", null, "fake"));
        Assert.Throws<DomainException>(() =>
            new PerspectiveSuggestion(note, PerspectiveKind.Critique, PerspectiveCategories.LogicalLeap, "Que passo falta?", null, "fake"));
        Assert.Throws<DomainException>(() =>
            new PerspectiveSuggestion(note, PerspectiveKind.Lens, PerspectiveCategories.Economic, "E a economia?", anchor, "fake"));
        Assert.Throws<DomainException>(() =>
            new PerspectiveSuggestion(note, PerspectiveKind.Lens, PerspectiveCategories.LogicalLeap, "E a lógica?", null, "fake"));
        Assert.Throws<DomainException>(() =>
            new PerspectiveSuggestion(note, PerspectiveKind.Critique, PerspectiveCategories.LogicalLeap, "Por quê?", anchor with { EndIndex = 4 }, "fake"));
    }
}
