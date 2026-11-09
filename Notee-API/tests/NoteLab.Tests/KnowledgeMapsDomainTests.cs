using NoteLab.Application;
using NoteLab.Domain;

namespace NoteLab.Tests;

public class BoardTests
{
    [Fact]
    public void New_board_trims_fields_and_starts_at_version_1()
    {
        var board = new Board("owner-1", "  Política  ", "   ");

        Assert.Equal("Política", board.Title);
        Assert.Null(board.Description);
        Assert.Equal(1, board.Version);
        Assert.NotEqual(Guid.Empty, board.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Board_requires_title(string title)
    {
        Assert.Throws<DomainException>(() => new Board("owner-1", title, null));
    }

    [Fact]
    public void Board_rejects_title_longer_than_80()
    {
        Assert.Throws<DomainException>(() => new Board("owner-1", new string('a', 81), null));
    }

    [Fact]
    public void Board_rejects_description_longer_than_240()
    {
        Assert.Throws<DomainException>(() => new Board("owner-1", "Mapa", new string('a', 241)));
    }

    [Fact]
    public void Board_requires_owner()
    {
        Assert.Throws<DomainException>(() => new Board(" ", "Mapa", null));
    }

    [Fact]
    public void UpdateDetails_increments_version()
    {
        var board = new Board("owner-1", "Mapa", null);

        board.UpdateDetails("Novo nome", "Descrição");

        Assert.Equal("Novo nome", board.Title);
        Assert.Equal("Descrição", board.Description);
        Assert.Equal(2, board.Version);
    }

    [Fact]
    public void Touch_does_not_change_version()
    {
        var board = new Board("owner-1", "Mapa", null);

        board.Touch();

        Assert.Equal(1, board.Version);
    }
}

public class BoardPlacementTests
{
    [Fact]
    public void MoveTo_updates_position_and_increments_version()
    {
        var placement = new BoardPlacement(Guid.NewGuid(), Guid.NewGuid(), 10, 20);

        placement.MoveTo(300.456, 400);

        Assert.Equal(300.46, placement.X);
        Assert.Equal(400, placement.Y);
        Assert.Equal(2, placement.PositionVersion);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(-1001, 0)]
    [InlineData(0, 7001)]
    public void Placement_rejects_invalid_coordinates(double x, double y)
    {
        Assert.Throws<DomainException>(() => new BoardPlacement(Guid.NewGuid(), Guid.NewGuid(), x, y));
    }

    [Fact]
    public void Placement_requires_board_and_note()
    {
        Assert.Throws<DomainException>(() => new BoardPlacement(Guid.Empty, Guid.NewGuid(), 0, 0));
        Assert.Throws<DomainException>(() => new BoardPlacement(Guid.NewGuid(), Guid.Empty, 0, 0));
    }
}

public class BoardEdgeTests
{
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [Fact]
    public void Edge_rejects_self_connection()
    {
        Assert.Throws<DomainException>(() => new BoardEdge(BoardId, A, A, EdgeRelationType.Related, null));
    }

    [Fact]
    public void Edge_preserves_direction_and_normalizes_blank_label()
    {
        var edge = new BoardEdge(BoardId, A, B, EdgeRelationType.None, "   ");

        Assert.Equal(A, edge.FromNoteId);
        Assert.Equal(B, edge.ToNoteId);
        Assert.Equal(EdgeRelationType.None, edge.RelationType);
        Assert.Null(edge.Label);
    }

    [Fact]
    public void Edge_rejects_label_longer_than_80()
    {
        Assert.Throws<DomainException>(() => new BoardEdge(BoardId, A, B, EdgeRelationType.Custom, new string('x', 81)));
    }

    [Fact]
    public void Edge_rejects_undefined_relation_type()
    {
        Assert.Throws<DomainException>(() => new BoardEdge(BoardId, A, B, (EdgeRelationType)99, null));
    }

    [Fact]
    public void Update_increments_version()
    {
        var edge = new BoardEdge(BoardId, A, B, EdgeRelationType.Related, null);

        edge.Update(B, A, EdgeRelationType.Causes, "porque");

        Assert.Equal(B, edge.FromNoteId);
        Assert.Equal(EdgeRelationType.Causes, edge.RelationType);
        Assert.Equal("porque", edge.Label);
        Assert.Equal(2, edge.Version);
    }

    [Fact]
    public void Equivalence_ignores_label_case_but_respects_direction()
    {
        var edge = new BoardEdge(BoardId, A, B, EdgeRelationType.Related, "Debate");

        Assert.True(edge.IsEquivalentTo(A, B, EdgeRelationType.Related, " debate "));
        Assert.False(edge.IsEquivalentTo(B, A, EdgeRelationType.Related, "Debate"));
        Assert.False(edge.IsEquivalentTo(A, B, EdgeRelationType.Causes, "Debate"));
    }

    [Theory]
    [InlineData("none", EdgeRelationType.None)]
    [InlineData("related", EdgeRelationType.Related)]
    [InlineData("Contradicts", EdgeRelationType.Contradicts)]
    [InlineData("custom", EdgeRelationType.Custom)]
    public void Relation_codes_parse(string code, EdgeRelationType expected)
    {
        Assert.True(EdgeRelationTypeCodes.TryParse(code, out EdgeRelationType type));
        Assert.Equal(expected, type);
        Assert.Equal(code.ToLowerInvariant(), type.ToCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("friendship")]
    public void Invalid_relation_codes_are_rejected(string? code)
    {
        Assert.False(EdgeRelationTypeCodes.TryParse(code, out _));
    }
}

public class NoteReferenceTests
{
    [Fact]
    public void Book_keeps_author_and_year_and_ignores_publisher()
    {
        var reference = new NoteReference("o", Guid.NewGuid(), NoteReferenceKind.Book, " Livro ", "Autora", "Editora", "2020", null);

        Assert.Equal("Livro", reference.Title);
        Assert.Equal("Autora", reference.Author);
        Assert.Equal("2020", reference.PublicationYear);
        Assert.Null(reference.Publisher);
    }

    [Fact]
    public void Source_keeps_publisher_and_ignores_author()
    {
        var reference = new NoteReference("o", Guid.NewGuid(), NoteReferenceKind.Source, "Artigo", "Autora", "Instituto", "2020", "https://example.org/a");

        Assert.Equal("Instituto", reference.Publisher);
        Assert.Null(reference.Author);
        Assert.Null(reference.PublicationYear);
        Assert.Equal("https://example.org/a", reference.Url);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("ftp://example.org")]
    [InlineData("not a url")]
    public void Reference_rejects_non_http_urls(string url)
    {
        Assert.Throws<DomainException>(() =>
            new NoteReference("o", Guid.NewGuid(), NoteReferenceKind.Source, "Fonte", null, null, null, url));
    }

    [Fact]
    public void Reference_requires_title()
    {
        Assert.Throws<DomainException>(() =>
            new NoteReference("o", Guid.NewGuid(), NoteReferenceKind.Book, " ", null, null, null, null));
    }
}

public class NoteTextTests
{
    [Fact]
    public void Extracts_text_from_editor_blocks()
    {
        string content = "[{\"id\":\"b1\",\"type\":\"paragraph\",\"text\":\"Primeira\"},{\"id\":\"b2\",\"type\":\"heading\",\"text\":\"Segunda\"}]";

        Assert.Equal("Primeira\nSegunda", NoteText.Extract(content, 100));
    }

    [Fact]
    public void Falls_back_to_plain_text()
    {
        Assert.Equal("[texto solto", NoteText.Extract("[texto solto", 100));
        Assert.Equal("Olá", NoteText.Extract("  Olá ", 100));
        Assert.Equal(string.Empty, NoteText.Extract(null, 100));
    }

    [Fact]
    public void Truncates_with_ellipsis()
    {
        Assert.Equal("abc…", NoteText.Extract("abcdef", 3));
    }
}
