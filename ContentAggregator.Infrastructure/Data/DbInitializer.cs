using ContentAggregator.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static void Initialize(DatabaseContext context)
        {
            context.Database.Migrate();

            if (context.Features.Any())
            {
                return;
            }

            var features = new Feature[]
            {
                new() { FirstNameEng = "Irakli", LastNameEng = "Gogava", FirstNameGeo = "ირაკლი", LastNameGeo = "გოგავა" },
                new() { FirstNameEng = "Soso", LastNameEng = "Manjavidze", FirstNameGeo = "სოსო", LastNameGeo = "მანჯავიძე" }
            };

            foreach (Feature feature in features)
            {
                context.Features.Add(feature);
            }

            context.SaveChanges();

            var ytChannels = new YTChannel[]
            {
                new()
                {
                    Name = "Salte",
                    Id = "UCIblVXoJdqdkIf694p3R6Wg",
                    Url = new Uri("https://www.youtube.com/@salte1481"),
                    ActivityLevel = ChannelActivityLevel.Medium
                }
            };

            foreach (YTChannel ytChannel in ytChannels)
            {
                context.YTChannels.Add(ytChannel);
            }

            context.SaveChanges();
        }
    }
}
