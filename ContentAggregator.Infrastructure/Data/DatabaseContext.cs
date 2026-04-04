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
        }
    }
}
