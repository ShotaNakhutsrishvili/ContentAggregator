using ContentAggregator.Worker.Jobs;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Worker.Hosting
{
    public sealed class BackgroundJobSchedulerHostedService : IHostedService
    {
        private readonly IRecurringJobManager _recurringJobs;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly IOptions<BackgroundJobOptions> _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<BackgroundJobSchedulerHostedService> _logger;

        public BackgroundJobSchedulerHostedService(
            IRecurringJobManager recurringJobs,
            IBackgroundJobClient backgroundJobs,
            IOptions<BackgroundJobOptions> options,
            IHostEnvironment environment,
            ILogger<BackgroundJobSchedulerHostedService> logger)
        {
            _recurringJobs = recurringJobs;
            _backgroundJobs = backgroundJobs;
            _options = options;
            _environment = environment;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            RegisterRecurringJobs();

            if (_environment.IsDevelopment() && _options.Value.RunStartupPipelineInDevelopment)
            {
                EnqueueStartupPipeline();
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void RegisterRecurringJobs()
        {
            var options = _options.Value;

            _recurringJobs.AddOrUpdate<YoutubeDiscoveryJob>(
                "pipeline:youtube-discovery",
                job => job.ProcessOnceAsync(),
                options.YoutubeDiscoveryCron);

            _recurringJobs.AddOrUpdate<SubtitleProcessingJob>(
                "pipeline:subtitle-fetch",
                job => job.ProcessOnceAsync(),
                options.SubtitleFetchCron);

            _recurringJobs.AddOrUpdate<SummaryGenerationJob>(
                "pipeline:georgian-summary",
                job => job.ProcessOnceAsync(),
                options.SummaryGenerationCron);

            _recurringJobs.AddOrUpdate<FacebookPublishingJob>(
                "pipeline:facebook-publish",
                job => job.ProcessOnceAsync(),
                options.FacebookPublishCron);

            _recurringJobs.AddOrUpdate<YoutubeCommentPublishingJob>(
                "pipeline:youtube-comment-publish",
                job => job.ProcessOnceAsync(),
                options.YoutubeCommentPublishCron);

            _logger.LogInformation("Registered recurring Hangfire jobs for the content pipeline.");
        }

        private void EnqueueStartupPipeline()
        {
            var discoveryJobId = _backgroundJobs.Enqueue<YoutubeDiscoveryJob>(job => job.ProcessOnceAsync());
            var subtitleJobId = _backgroundJobs.ContinueJobWith<SubtitleProcessingJob>(
                discoveryJobId,
                job => job.ProcessOnceAsync());
            var summaryJobId = _backgroundJobs.ContinueJobWith<SummaryGenerationJob>(
                subtitleJobId,
                job => job.ProcessOnceAsync());

            _backgroundJobs.ContinueJobWith<FacebookPublishingJob>(summaryJobId, job => job.ProcessOnceAsync());
            _backgroundJobs.ContinueJobWith<YoutubeCommentPublishingJob>(summaryJobId, job => job.ProcessOnceAsync());

            _logger.LogInformation("Enqueued the development startup content pipeline.");
        }
    }
}
