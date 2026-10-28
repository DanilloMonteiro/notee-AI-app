namespace NoteLab.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

public sealed class NoteDbContext : IdentityDbContext
{
    public DbSet<Note> Notes { get; set; }

    public NoteDbContext(DbContextOptions<NoteDbContext> variavel) : base(variavel)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>().Property(n => n.Title).HasConversion(title => title.Value, texto => new NoteTitle(texto));
        modelBuilder.Entity<Note>().Property(n => n.Version).IsConcurrencyToken();
    }
}