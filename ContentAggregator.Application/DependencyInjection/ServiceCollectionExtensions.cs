using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Services.Facebook;
using ContentAggregator.Application.Services.Features;
using ContentAggregator.Application.Services.Subtitles;
using ContentAggregator.Application.Services.Summarization;
using ContentAggregator.Application.Services.Youtube;
using ContentAggregator.Application.Services.YoutubeComments;
using ContentAggregator.Application.Services.YoutubeContents;
using Microsoft.Extensions.DependencyInjection;

namespace ContentAggregator.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IFeatureService, FeatureService>();
            services.AddScoped<IFacebookPostService, FacebookPostService>();
            services.AddScoped<IFacebookPublishingWorkflow, FacebookPublishingWorkflow>();
            services.AddScoped<ISubtitleWorkflow, SubtitleWorkflow>();
            services.AddScoped<ISummarizationWorkflow, SummarizationWorkflow>();
            services.AddScoped<IYoutubeChannelService, YoutubeChannelService>();
            services.AddScoped<IYoutubeCommentWorkflow, YoutubeCommentWorkflow>();
            services.AddScoped<IYoutubeDiscoveryWorkflow, YoutubeDiscoveryWorkflow>();
            services.AddScoped<IYoutubeContentQueryService, YoutubeContentQueryService>();

            return services;
        }
    }
}
