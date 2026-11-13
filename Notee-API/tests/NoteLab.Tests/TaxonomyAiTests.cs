using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

public class TaxonomyAiTests
{
    [Fact]
    public void Parse_normalizes_existing_names_removes_duplicates_and_limits_tags()
    {
        var category = Category.Create("owner", "Economia");
        var tag = Tag.Create("owner", "Inflação");
        const string json = """
            {"category":{"name":"economia","reason":"assunto"},"tags":[{"name":"inflacao","reason":"texto"},{"name":"Inflação","reason":"duplicada"},{"name":"juros","reason":"texto"},{"name":"câmbio","reason":"texto"},{"name":"política","reason":"texto"},{"name":"mercado","reason":"texto"},{"name":"extra","reason":"texto"}]}
            """;
        var result = TaxonomyAi.Parse(json, [category], [tag]);
        Assert.Equal(category.Id, result.Category?.ExistingId);
        Assert.Equal(tag.Id, result.Tags[0].ExistingId);
        Assert.Equal(5, result.Tags.Count);
        Assert.Single(result.Tags, t => t.ExistingId == tag.Id);
    }

    [Fact]
    public void Fake_uses_local_note_content_without_external_provider()
    {
        var note = new Note(new NoteTitle("Economia brasileira"), "owner", null);
        note.SetPlainText("Inflação e crescimento. Inflação e crescimento.");
        var result = TaxonomyAi.Fake(note, [Category.Create("owner", "Economia")], []);
        Assert.False(result.Category?.IsNew);
        Assert.Contains(result.Tags, t => t.Name == "inflacao" && t.IsNew);
    }
}
