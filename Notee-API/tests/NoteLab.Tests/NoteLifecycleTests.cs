using NoteLab.Domain;

namespace NoteLab.Tests;

public class NoteLifecycleTests
{
    private static Note NewNote() => new(new NoteTitle("Nota"), "owner", "texto");

    [Fact]
    public void Archive_then_restore_returns_to_active_and_bumps_version()
    {
        Note note = NewNote();

        note.Archive();
        Assert.Equal(NoteStatus.Archived, note.Status);
        note.Restore();

        Assert.Equal(NoteStatus.Active, note.Status);
        Assert.Equal(3, note.Version);
    }

    [Fact]
    public void Trash_sets_timestamp_and_blocks_editing()
    {
        Note note = NewNote();

        note.MoveToTrash();

        Assert.Equal(NoteStatus.Trashed, note.Status);
        Assert.NotNull(note.TrashedAt);
        Assert.Throws<DomainException>(() => note.Update(new NoteTitle("x"), "y"));
        Assert.Throws<DomainException>(() => note.Archive());
        Assert.Throws<DomainException>(() => note.MoveToTrash());
    }

    [Fact]
    public void Restore_from_trash_clears_timestamp()
    {
        Note note = NewNote();
        note.MoveToTrash();

        note.Restore();

        Assert.Null(note.TrashedAt);
        Assert.True(note.IsEditable);
    }

    [Fact]
    public void Restore_requires_archived_or_trashed()
    {
        Assert.Throws<DomainException>(() => NewNote().Restore());
    }

    [Fact]
    public void Tags_can_be_cleared_and_category_removed()
    {
        Note note = NewNote();
        note.UpdateTags([Tag.Create("owner", "a")]);
        note.SetCategory(Category.Create("owner", "c"));

        note.ClearTags();
        note.ClearCategory();

        Assert.Empty(note.Tags);
        Assert.Null(note.CategoryId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Taxonomy_names_are_validated(string name)
    {
        Assert.Throws<DomainException>(() => Category.Create("owner", name));
        Assert.Throws<DomainException>(() => Tag.Create("owner", name));
        Assert.Throws<DomainException>(() => Tag.Create("owner", new string('x', 61)));
    }
}
