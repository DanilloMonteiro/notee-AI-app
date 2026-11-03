namespace NoteLab.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

public sealed class NoteDbContext : IdentityDbContext
{
    public DbSet<Note> Notes { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Doubt> Doubts { get; set; }

    public NoteDbContext(DbContextOptions<NoteDbContext> variavel) : base(variavel)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>().Property(n => n.Title).HasConversion(title => title.Value, texto => new NoteTitle(texto));
        modelBuilder.Entity<Note>().Property(n => n.Version).IsConcurrencyToken();
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
    }
}