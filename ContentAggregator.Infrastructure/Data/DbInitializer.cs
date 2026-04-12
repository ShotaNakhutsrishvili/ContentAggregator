using ContentAggregator.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static Task ApplyMigrationsAsync(
            DatabaseContext context,
            CancellationToken cancellationToken = default)
        {
            return context.Database.MigrateAsync(cancellationToken);
        }

        public static async Task SeedDevelopmentDataAsync(
            DatabaseContext context,
            CancellationToken cancellationToken = default)
        {
            var features = new Feature[]
            {
                new() { FirstNameEng = "Irakli", LastNameEng = "Gogava", FirstNameGeo = "ირაკლი", LastNameGeo = "გოგავა" },
                new() { FirstNameEng = "Soso", LastNameEng = "Manjavidze", FirstNameGeo = "სოსო", LastNameGeo = "მანჯავიძე" }
            };

            foreach (var feature in features)
            {
                var exists = await context.Features.AnyAsync(
                    existing =>
                        existing.FirstNameEng == feature.FirstNameEng
                        && existing.LastNameEng == feature.LastNameEng,
                    cancellationToken);

                if (!exists)
                {
                    context.Features.Add(feature);
                }
            }

            if (!await context.YTChannels.AnyAsync(
                    channel => channel.Id == "UCIblVXoJdqdkIf694p3R6Wg",
                    cancellationToken))
            {
                context.YTChannels.Add(new YTChannel
                {
                    Name = "Salte",
                    Id = "UCIblVXoJdqdkIf694p3R6Wg",
                    Url = new Uri("https://www.youtube.com/@salte1481"),
                    ActivityLevel = ChannelActivityLevel.Medium
                });
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
