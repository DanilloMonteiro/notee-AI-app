namespace NoteLab.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

public sealed class NoteDbContext : IdentityDbContext
{
    public DbSet<Note> Notes { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Doubt> Doubts { get; set; }
    public DbSet<NoteReference> NoteReferences { get; set; }
    public DbSet<UserPreferences> UserPreferences { get; set; }
    public DbSet<NoteRevision> NoteRevisions { get; set; }

    // Módulo KnowledgeMaps (Mapas de Ideias)
    public DbSet<Board> Boards { get; set; }
    public DbSet<BoardPlacement> BoardPlacements { get; set; }
    public DbSet<BoardEdge> BoardEdges { get; set; }

    public NoteDbContext(DbContextOptions<NoteDbContext> variavel) : base(variavel)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>().Property(n => n.Title).HasConversion(title => title.Value, texto => new NoteTitle(texto));
        modelBuilder.Entity<Note>().Property(n => n.Version).IsConcurrencyToken();
        modelBuilder.Entity<Note>().HasIndex(n => new { n.OwnerId, n.Status, n.UpdatedAt });
        modelBuilder.Entity<Note>().Navigation(n => n.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<Note>().HasMany(n => n.Tags).WithMany().UsingEntity("NoteTags");
        modelBuilder.Entity<Note>().Metadata.FindNavigation(nameof(Note.Tags))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<Doubt>().OwnsOne(d => d.Anchor, a =>
        {
            // Opcional: Renomear as colunas para ficarem mais limpas no PostgreSQL
            a.Property(p => p.BlockId).HasColumnName("AnchorBlockId");
            a.Property(p => p.ExactQuote).HasColumnName("AnchorExactQuote");
            a.Property(p => p.StartIndex).HasColumnName("AnchorStartIndex");
            a.Property(p => p.EndIndex).HasColumnName("AnchorEndIndex");
        });

        modelBuilder.Entity<NoteReference>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.OwnerId).IsRequired();
            e.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Title).HasMaxLength(NoteReference.TitleMaxLength).IsRequired();
            e.Property(r => r.Author).HasMaxLength(NoteReference.PersonMaxLength);
            e.Property(r => r.Publisher).HasMaxLength(NoteReference.PersonMaxLength);
            e.Property(r => r.PublicationYear).HasMaxLength(NoteReference.YearMaxLength);
            e.Property(r => r.Url).HasMaxLength(NoteReference.UrlMaxLength);
            e.HasOne<Note>().WithMany().HasForeignKey(r => r.NoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.OwnerId, r.NoteId });
        });

        modelBuilder.Entity<NoteRevision>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.OwnerId).IsRequired();
            e.Property(r => r.Title).HasMaxLength(100).IsRequired();
            e.Property(r => r.TagIds).IsRequired();
            e.Property(r => r.Reason).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(r => new { r.NoteId, r.NoteVersion }).IsUnique();
            e.HasOne<Note>().WithMany().HasForeignKey(r => r.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreferences>(e =>
        {
            e.HasKey(p => p.UserId);
            e.Property(p => p.Version).IsConcurrencyToken();
            e.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>().WithOne().HasForeignKey<UserPreferences>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureKnowledgeMaps(modelBuilder);
    }

    private static void ConfigureKnowledgeMaps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.OwnerId).IsRequired();
            e.Property(b => b.Title).HasMaxLength(Board.TitleMaxLength).IsRequired();
            e.Property(b => b.Description).HasMaxLength(Board.DescriptionMaxLength);
            e.Property(b => b.Version).IsConcurrencyToken();
            e.HasIndex(b => new { b.OwnerId, b.UpdatedAt });
        });

        modelBuilder.Entity<BoardPlacement>(e =>
        {
            // Uma posição por nota em cada mapa.
            e.HasKey(p => new { p.BoardId, p.NoteId });
            e.Property(p => p.PositionVersion).IsConcurrencyToken();
            e.HasOne<Board>().WithMany().HasForeignKey(p => p.BoardId).OnDelete(DeleteBehavior.Cascade);
            // Excluir a nota definitivamente remove suas posições em todos os mapas.
            e.HasOne<Note>().WithMany().HasForeignKey(p => p.NoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => p.NoteId);
            e.ToTable(t => t.HasCheckConstraint(
                "CK_BoardPlacements_Coordinates",
                $"\"X\" BETWEEN {BoardPlacement.MinCoordinate} AND {BoardPlacement.MaxCoordinate} AND \"Y\" BETWEEN {BoardPlacement.MinCoordinate} AND {BoardPlacement.MaxCoordinate}"));
        });

        modelBuilder.Entity<BoardEdge>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RelationType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Label).HasMaxLength(BoardEdge.LabelMaxLength);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Note>().WithMany().HasForeignKey(x => x.FromNoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Note>().WithMany().HasForeignKey(x => x.ToNoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BoardId, x.FromNoteId });
            e.HasIndex(x => new { x.BoardId, x.ToNoteId });
            e.ToTable(t => t.HasCheckConstraint("CK_BoardEdges_NoSelfEdge", "\"FromNoteId\" <> \"ToNoteId\""));
        });
    }
}