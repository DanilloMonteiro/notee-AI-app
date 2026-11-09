using NoteLab.Domain;

namespace NoteLab.Tests;

public class PublishingTests
{
    [Theory]
    [InlineData("Democracia Direta!", "democracia-direta")]
    [InlineData("  Ação & Reação  ", "acao-reacao")]
    [InlineData("a---b", "a-b")]
    public void Slug_is_normalized(string input, string expected)
    {
        Assert.Equal(expected, Publication.NormalizeSlug(input));
    }

    [Fact]
    public void Slug_without_letters_is_rejected()
    {
        Assert.Throws<DomainException>(() => Publication.NormalizeSlug("!!!"));
    }

    [Fact]
    public void Revision_is_a_snapshot_of_the_note()
    {
        var note = new Note(new NoteTitle("Original"), "owner", "conteúdo v1");
        var publication = new Publication("owner", note.Id, "original", PublicationVisibility.Public);
        var revision = new PublicationRevision(publication.Id, note);

        note.Update(new NoteTitle("Editada"), "conteúdo v2");

        Assert.Equal("Original", revision.Title);
        Assert.Equal("conteúdo v1", revision.Content);
    }

    [Fact]
    public void Revoked_publication_cannot_change()
    {
        var publication = new Publication("owner", Guid.NewGuid(), "x", PublicationVisibility.Unlisted);
        publication.Revoke();

        Assert.False(publication.IsLive);
        Assert.Throws<DomainException>(() => publication.ChangeVisibility(PublicationVisibility.Public));
        Assert.Throws<DomainException>(() => publication.RotateToken());
        Assert.Throws<DomainException>(() => publication.Revoke());
    }

    [Fact]
    public void Rotate_token_changes_the_secret_link()
    {
        var publication = new Publication("owner", Guid.NewGuid(), "x", PublicationVisibility.Unlisted);
        string before = publication.ShareToken;

        publication.RotateToken();

        Assert.NotEqual(before, publication.ShareToken);
        Assert.True(publication.ShareToken.Length >= 30);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Com Espaço")]
    [InlineData("acentuação")]
    public void Invalid_handles_are_rejected(string handle)
    {
        Assert.Throws<DomainException>(() => new PublicProfile("u", handle, "Nome"));
    }

    [Fact]
    public void Handle_is_lowercased()
    {
        Assert.Equal("alex_costa", new PublicProfile("u", "Alex_Costa", "Alex").Handle);
    }
}
