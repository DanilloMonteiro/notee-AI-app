using NoteLab.Domain;

namespace NoteLab.Tests;

public class NoteRevisionTests
{
    [Fact]
    public void Capture_copies_content_tags_and_version()
    {
        var note = new Note(new NoteTitle("Título"), "owner", "conteúdo");
        Tag a = Tag.Create("owner", "a");
        Tag b = Tag.Create("owner", "b");
        note.UpdateTags([b, a]);
        note.RegisterUpdate();

        NoteRevision revision = NoteRevision.Capture(note, RevisionReason.Edited);

        Assert.Equal(2, revision.NoteVersion);
        Assert.Equal("Título", revision.Title);
        Assert.Equal("conteúdo", revision.Content);
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(x => x), revision.GetTagIds());
    }

    [Fact]
    public void Snapshot_is_not_affected_by_later_changes()
    {
        var note = new Note(new NoteTitle("Antes"), "owner", "v1");
        NoteRevision revision = NoteRevision.Capture(note, RevisionReason.Created);

        note.Update(new NoteTitle("Depois"), "v2");

        Assert.Equal("Antes", revision.Title);
        Assert.Equal("v1", revision.Content);
    }

    [Fact]
    public void Empty_tags_round_trip()
    {
        var note = new Note(new NoteTitle("x"), "owner", null);

        Assert.Empty(NoteRevision.Capture(note, RevisionReason.Created).GetTagIds());
    }
}
