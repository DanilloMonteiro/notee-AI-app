namespace NoteLab.Domain;

using Microsoft.EntityFrameworkCore;
using NoteLab.Application;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

public sealed class NoteDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<PublicationView> PublicationViews { get; set; }
    public DbSet<Community> Communities { get; set; }
    public DbSet<CommunityMember> CommunityMembers { get; set; }
    public DbSet<CommunityPublication> CommunityPublications { get; set; }
    public DbSet<PublicationComment> PublicationComments { get; set; }
    public DbSet<SocialFollow> SocialFollows { get; set; }
    public DbSet<SocialReaction> SocialReactions { get; set; }
    public DbSet<Note> Notes { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Doubt> Doubts { get; set; }
    public DbSet<NoteReference> NoteReferences { get; set; }
    public DbSet<UserPreferences> UserPreferences { get; set; }
    public DbSet<NoteRevision> NoteRevisions { get; set; }
    public DbSet<ResearchJob> ResearchJobs { get; set; }
    public DbSet<GraphSuggestion> GraphSuggestions { get; set; }
    public DbSet<Publication> Publications { get; set; }
    public DbSet<PublicationRevision> PublicationRevisions { get; set; }
    public DbSet<PublicProfile> PublicProfiles { get; set; }
    public DbSet<NoteSuggestion> NoteSuggestions { get; set; }
    public DbSet<NoteSettings> NoteSettings { get; set; }
    public DbSet<RefreshSession> RefreshSessions { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<ResearchSource> ResearchSources { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<AiProviderSecret> AiProviderSecrets { get; set; }

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

        modelBuilder.Entity<ApplicationUser>().Property(u => u.FullName).HasMaxLength(ApplicationUser.FullNameMaxLength);

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

        modelBuilder.Entity<Doubt>(e =>
        {
            e.Navigation(d => d.Anchor).IsRequired(false);
            e.Property(d => d.Origin).HasConversion<string>().HasMaxLength(20).HasDefaultValue(DoubtOrigin.Selection);
            e.Property(d => d.Evidence).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.AnswerProvider).HasMaxLength(20);
            e.HasIndex(d => new { d.OwnerId, d.NoteId });
        });

        modelBuilder.Entity<ResearchJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.OwnerId).IsRequired();
            e.Property(j => j.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(j => j.LeaseOwner).HasMaxLength(64);
            e.Property(j => j.IdempotencyKey).HasMaxLength(100);
            e.Property(j => j.Provider).HasMaxLength(20);
            e.Property(j => j.LastError).HasMaxLength(300);
            e.Property(j => j.FencingToken).IsConcurrencyToken();
            e.HasOne<Doubt>().WithMany().HasForeignKey(j => j.DoubtId).OnDelete(DeleteBehavior.Cascade);
            // Fila: busca por prontos e por leases vencidos.
            e.HasIndex(j => new { j.Status, j.NextAttemptAt });
            // No máximo um job ativo por dúvida.
            e.HasIndex(j => j.DoubtId).IsUnique().HasFilter("\"Status\" IN ('Queued', 'Running')").HasDatabaseName("IX_ResearchJobs_ActivePerDoubt");
            // Idempotency-Key única por dono.
            e.HasIndex(j => new { j.OwnerId, j.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });

        modelBuilder.Entity<ResearchSource>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Title).HasMaxLength(300).IsRequired();
            e.Property(s => s.Url).HasMaxLength(2048).IsRequired();
            e.HasOne<Doubt>().WithMany().HasForeignKey(s => s.DoubtId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.DoubtId, s.Position });
        });

        modelBuilder.Entity<AiProviderSecret>(e =>
        {
            e.HasKey(s => s.ProviderId);
            e.Property(s => s.ProviderId).HasMaxLength(32);
            e.Property(s => s.ProtectedKey).IsRequired();
            e.Property(s => s.UpdatedBy).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100);
            e.Property(s => s.Value).HasMaxLength(200).IsRequired();
            e.Property(s => s.UpdatedBy).HasMaxLength(256).IsRequired();
        });

        // ---- Outbox / Inbox (M11) ----
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).HasMaxLength(100).IsRequired();
            e.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            e.Property(m => m.LastError).HasMaxLength(300);
            e.HasIndex(m => m.OccurredAt).HasFilter("\"ProcessedAt\" IS NULL").HasDatabaseName("IX_OutboxMessages_Pending");
        });

        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.HasKey(m => new { m.Consumer, m.MessageId });
            e.Property(m => m.Consumer).HasMaxLength(100);
        });

        // ---- Sessões de refresh (M04) ----
        modelBuilder.Entity<RefreshSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.UserId).IsRequired();
            e.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.HasIndex(s => s.FamilyId);
            e.HasIndex(s => s.UserId);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Análise assistida e configuração por nota (M10) ----
        modelBuilder.Entity<NoteSuggestion>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.OwnerId).IsRequired();
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Value).HasMaxLength(NoteSuggestion.ValueMaxLength).IsRequired();
            e.Property(s => s.Reason).HasMaxLength(300);
            e.Property(s => s.Provider).HasMaxLength(20);
            e.HasOne<Note>().WithMany().HasForeignKey(s => s.NoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.NoteId, s.Status });
        });

        modelBuilder.Entity<NoteSettings>(e =>
        {
            e.HasKey(s => s.NoteId);
            e.Property(s => s.OwnerId).IsRequired();
            e.Property(s => s.AutoResearch).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Version).IsConcurrencyToken();
            e.HasOne<Note>().WithOne().HasForeignKey<NoteSettings>(s => s.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Publishing (M08) ----
        modelBuilder.Entity<Publication>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.OwnerId).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(Publication.SlugMaxLength).IsRequired();
            e.Property(p => p.ShareToken).HasMaxLength(64).IsRequired();
            e.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Version).IsConcurrencyToken();
            e.HasIndex(p => p.ShareToken).IsUnique();
            // Endereço único por autor entre publicações ativas.
            e.HasIndex(p => new { p.OwnerId, p.Slug }).IsUnique().HasFilter("\"Status\" = 'Published'");
            // Excluir a nota definitivamente remove suas publicações.
            e.HasOne<Note>().WithMany().HasForeignKey(p => p.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PublicationRevision>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(100).IsRequired();
            e.HasOne<Publication>().WithMany().HasForeignKey(r => r.PublicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.PublicationId, r.PublishedAt });
        });

        modelBuilder.Entity<PublicProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.Property(p => p.Handle).HasMaxLength(PublicProfile.HandleMaxLength).IsRequired();
            e.Property(p => p.DisplayName).HasMaxLength(PublicProfile.DisplayNameMaxLength).IsRequired();
            e.HasIndex(p => p.Handle).IsUnique();
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<PublicProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
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
            // Contas existentes continuam com pesquisa automática ligada; true é o "não informado" do EF.
            e.Property(p => p.AutoResearch).HasDefaultValue(true).HasSentinel(true);
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserPreferences>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SocialFollow>(e => {
            e.HasKey(x => new { x.UserId, x.TargetId });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TargetId);
        });
        modelBuilder.Entity<SocialReaction>(e => {
            e.HasKey(x => new { x.UserId, x.PublicationId, x.Kind });
            e.Property(x => x.Kind).HasMaxLength(16);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Publication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PublicationId, x.Kind });
        });
        modelBuilder.Entity<Community>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CommunityMember>(e => {
            e.HasKey(x => new { x.CommunityId, x.UserId });
            e.HasOne<Community>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CommunityPublication>(e => {
            e.HasKey(x => new { x.CommunityId, x.PublicationId });
            e.HasOne<Community>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Publication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PublicationView>(e => {
            e.HasKey(x => new { x.PublicationId, x.UserId, x.Day });
            e.HasOne<Publication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PublicationComment>(e => {
            e.HasOne<PublicationComment>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasKey(x => x.Id); e.Property(x => x.Text).HasMaxLength(2000);
            e.HasOne<Publication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PublicationRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PublicationId, x.CreatedAt });
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

        modelBuilder.Entity<GraphSuggestion>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.OwnerId).IsRequired();
            e.Property(s => s.RelationType).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Reason).HasMaxLength(GraphSuggestion.ReasonMaxLength);
            e.Property(s => s.ContextHash).HasMaxLength(64);
            e.Property(s => s.Provider).HasMaxLength(20);
            e.HasOne<Board>().WithMany().HasForeignKey(s => s.BoardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Note>().WithMany().HasForeignKey(s => s.FromNoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Note>().WithMany().HasForeignKey(s => s.ToNoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.BoardId, s.Status });
        });
    }
}