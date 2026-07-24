using Microsoft.EntityFrameworkCore;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Infrastructure.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
        }

        public DbSet<YoutubeContent> YoutubeContents { get; set; }
        public DbSet<YTChannel> YTChannels { get; set; }
        public DbSet<Feature> Features { get; set; }
        public DbSet<YoutubeContentFeature> YoutubeContentFeatures { get; set; }
        public DbSet<YoutubeContentRevision> YoutubeContentRevisions { get; set; }
        public DbSet<YoutubeContentSection> YoutubeContentSections { get; set; }
        public DbSet<YoutubeContentPublication> YoutubeContentPublications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.DisplayName());
            }

            modelBuilder.Entity<YTChannel>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id)
                    .HasMaxLength(100)
                    .ValueGeneratedNever(); // Disable DB generation
                entity.Property(p => p.ActivityLevel)
                    .HasConversion<byte>();
            });

            modelBuilder.Entity<YoutubeContent>()
                .HasMany(e => e.Features)
                .WithMany(e => e.YoutubeContents)
                .UsingEntity<YoutubeContentFeature>();

            modelBuilder.Entity<YoutubeContent>()
                .HasOne(yc => yc.YTChannel)
                .WithMany(yt => yt.YoutubeContents)
                .HasForeignKey(yc => yc.ChannelId);

            modelBuilder.Entity<YoutubeContent>()
                .Property(yc => yc.SubtitleLanguage)
                .HasConversion<byte>();

            modelBuilder.Entity<YoutubeContent>()
                .HasIndex(yc => yc.VideoId)
                .IsUnique();

            modelBuilder.Entity<YoutubeContentRevision>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasAlternateKey(x => new { x.YoutubeContentId, x.Id });
                entity.HasIndex(x => new { x.YoutubeContentId, x.Version })
                    .IsUnique();
                entity.Property(x => x.Language)
                    .HasConversion<byte>();
                entity.Property(x => x.ReviewState)
                    .HasConversion<byte>();
                entity.Property(x => x.TranscriptChecksum)
                    .HasMaxLength(128);
                entity.Property(x => x.GeneratorModel)
                    .HasMaxLength(200);
                entity.Property(x => x.PromptVersion)
                    .HasMaxLength(50);
                entity.Property(x => x.ReviewedBy)
                    .HasMaxLength(200);
                entity.Property(x => x.ConcurrencyVersion)
                    .IsRowVersion();
                entity.HasOne(x => x.YoutubeContent)
                    .WithMany(x => x.Revisions)
                    .HasForeignKey(x => x.YoutubeContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Navigation(x => x.Sections)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_YoutubeContentRevision_Version",
                        "\"Version\" > 0");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentRevision_TranscriptChecksum",
                        "length(btrim(\"TranscriptChecksum\")) > 0");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentRevision_ReviewState",
                        "\"ReviewState\" BETWEEN 0 AND 4");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentRevision_ApprovedReview",
                        "\"ReviewState\" <> 3 OR "
                        + "(\"ReviewedAt\" IS NOT NULL "
                        + "AND \"ReviewedBy\" IS NOT NULL "
                        + "AND length(btrim(\"ReviewedBy\")) > 0)");
                });
            });

            modelBuilder.Entity<YoutubeContentSection>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.YoutubeContentRevisionId, x.Ordinal })
                    .IsUnique();
                entity.Property(x => x.Heading)
                    .HasMaxLength(300);
                entity.Property(x => x.Source)
                    .HasConversion<byte>();
                entity.Property(x => x.Confidence)
                    .HasPrecision(5, 4);
                entity.HasOne(x => x.YoutubeContentRevision)
                    .WithMany(x => x.Sections)
                    .HasForeignKey(x => x.YoutubeContentRevisionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_Ordinal",
                        "\"Ordinal\" >= 0");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_StartSeconds",
                        "\"StartSeconds\" >= 0");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_EndSeconds",
                        "\"EndSeconds\" IS NULL OR \"EndSeconds\" > \"StartSeconds\"");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_Heading",
                        "length(btrim(\"Heading\")) > 0");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_Source",
                        "\"Source\" BETWEEN 0 AND 1");
                    table.HasCheckConstraint(
                        "CK_YoutubeContentSection_Confidence",
                        "\"Confidence\" IS NULL OR \"Confidence\" BETWEEN 0 AND 1");
                });
            });

            modelBuilder.Entity<YoutubeContentPublication>(entity =>
            {
                entity.HasKey(x => x.YoutubeContentId);
                entity.Property(x => x.State)
                    .HasConversion<byte>();
                entity.Property(x => x.WithdrawalReason)
                    .HasMaxLength(1000);
                entity.Property(x => x.ConcurrencyVersion)
                    .IsRowVersion();
                entity.HasOne(x => x.YoutubeContent)
                    .WithOne(x => x.Publication)
                    .HasForeignKey<YoutubeContentPublication>(x => x.YoutubeContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.PublishedRevision)
                    .WithOne(x => x.Publication)
                    .HasForeignKey<YoutubeContentPublication>(
                        x => new { x.YoutubeContentId, x.PublishedRevisionId })
                    .HasPrincipalKey<YoutubeContentRevision>(
                        x => new { x.YoutubeContentId, x.Id })
                    .OnDelete(DeleteBehavior.NoAction);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_YoutubeContentPublication_State",
                        "(\"State\" = 0 "
                        + "AND \"PublishedRevisionId\" IS NULL "
                        + "AND \"PublishedAt\" IS NULL "
                        + "AND \"WithdrawnAt\" IS NULL "
                        + "AND \"WithdrawalReason\" IS NULL) "
                        + "OR (\"State\" = 1 "
                        + "AND \"PublishedRevisionId\" IS NOT NULL "
                        + "AND \"PublishedAt\" IS NOT NULL "
                        + "AND \"WithdrawnAt\" IS NULL "
                        + "AND \"WithdrawalReason\" IS NULL) "
                        + "OR (\"State\" = 2 "
                        + "AND \"PublishedRevisionId\" IS NOT NULL "
                        + "AND \"PublishedAt\" IS NOT NULL "
                        + "AND \"WithdrawnAt\" IS NOT NULL "
                        + "AND \"WithdrawalReason\" IS NOT NULL "
                        + "AND length(btrim(\"WithdrawalReason\")) > 0)");
                });
            });
        }
    }
}
