using ContentAggregator.Application.DependencyInjection;
using ContentAggregator.Infrastructure.DependencyInjection;
using ContentAggregator.Worker.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentAggregator.Worker.Hosting
{
    public static class WorkerHostExtensions
    {
        public static HostApplicationBuilder AddWorkerBootstrapServices(this HostApplicationBuilder builder)
        {
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddDatabaseBootstrapServices();

            return builder;
        }

        public static HostApplicationBuilder AddWorkerHostServices(this HostApplicationBuilder builder)
        {
            builder.Services.AddApplicationServices();
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddBackgroundProcessing(builder.Configuration);

            return builder;
        }

        private static IServiceCollection AddBackgroundProcessing(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<YoutubeDiscoveryJob>();
            services.AddScoped<SubtitleProcessingJob>();
            services.AddScoped<SummaryGenerationJob>();
            services.AddScoped<FacebookPublishingJob>();
            services.AddScoped<YoutubeCommentPublishingJob>();

            services.AddOptions<BackgroundJobOptions>()
                .Bind(configuration.GetSection(BackgroundJobOptions.SectionName));

            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UsePostgreSqlStorage(storage =>
                        storage.UseNpgsqlConnection(configuration.GetConnectionString("PostgreSQLConnection")));
            });
            services.AddHangfireServer();
            services.AddHostedService<BackgroundJobSchedulerHostedService>();

            return services;
        }
    }
}
