using ContentAggregator.Application.Interfaces;
using ContentAggregator.Infrastructure.Data;
using ContentAggregator.Infrastructure.Repositories;
using ContentAggregator.Infrastructure.Services.Facebook;
using ContentAggregator.Infrastructure.Services.Subtitles;
using ContentAggregator.Infrastructure.Services.Summarization;
using ContentAggregator.Infrastructure.Services.Youtube;
using ContentAggregator.Infrastructure.Services.YoutubeComments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<DatabaseContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection")));

            services.AddScoped<IFeatureRepository, FeatureRepository>();
            services.AddScoped<IYTChannelRepository, YTChannelRepository>();
            services.AddScoped<IYoutubeContentRepository, YoutubeContentRepository>();
            services.AddScoped<ISubtitleDownloader, YtDlpSubtitleDownloader>();

            services.AddOptions<FacebookOptions>()
                .Bind(configuration.GetSection(FacebookOptions.SectionName));
            services.AddOptions<YtDlpOptions>()
                .Bind(configuration.GetSection(YtDlpOptions.SectionName));
            services.AddOptions<LmStudioOptions>()
                .Bind(configuration.GetSection(LmStudioOptions.SectionName));
            services.AddOptions<YoutubeApiOptions>()
                .Bind(configuration.GetSection(YoutubeApiOptions.SectionName));
            services.AddOptions<YoutubeCommentOptions>()
                .Bind(configuration.GetSection(YoutubeCommentOptions.SectionName));

            services.AddHttpClient<IFacebookPublisher, FacebookPublisher>(client =>
            {
                client.BaseAddress = new Uri("https://graph.facebook.com/");
                client.Timeout = TimeSpan.FromMinutes(2);
            });
            services.AddHttpClient<ISummaryGenerator, LmStudioSummaryGenerator>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<LmStudioOptions>>().Value;
                client.Timeout = TimeSpan.FromMinutes(40);

                if (!string.IsNullOrWhiteSpace(options.BaseUrl)
                    && Uri.TryCreate(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute, out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }
            });
            services.AddHttpClient<IYoutubeMetadataClient, YoutubeMetadataClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
                client.Timeout = TimeSpan.FromMinutes(2);
            });
            services.AddHttpClient<IYoutubeCommentPublisher, YoutubeCommentPublisher>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
                client.Timeout = TimeSpan.FromMinutes(2);
            });

            return services;
        }

        public static IServiceCollection AddDatabaseBootstrapServices(this IServiceCollection services)
        {
            services.AddScoped<DatabaseBootstrapper>();

            return services;
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.TrimEnd('/') + "/";
        }
    }
}
