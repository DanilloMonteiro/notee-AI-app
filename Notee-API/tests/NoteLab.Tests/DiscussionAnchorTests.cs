using NoteLab.Application;

namespace NoteLab.Tests;

public class DiscussionAnchorTests
{
    private const string Content = """[{"id":"b1","type":"paragraph","text":"Trecho publicado para comentar"}]""";

    [Fact]
    public void Anchor_matches_exact_text_in_published_block() =>
        Assert.True(DiscussionEndpoints.ValidAnchor(Content, new("b1", "Trecho publicado", 0)));

    [Theory]
    [InlineData("b2", "Trecho publicado", 0)]
    [InlineData("b1", "Trecho publicado", 1)]
    [InlineData("b1", "Trecho removido", 0)]
    public void Anchor_rejects_wrong_block_position_or_quote(string blockId, string quote, int start) =>
        Assert.False(DiscussionEndpoints.ValidAnchor(Content, new(blockId, quote, start)));
}
