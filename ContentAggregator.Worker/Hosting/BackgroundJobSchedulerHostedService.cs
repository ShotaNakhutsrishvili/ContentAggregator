using ContentAggregator.Worker.Jobs;
using ContentAggregator.Infrastructure.Services.Facebook;
using ContentAggregator.Infrastructure.Services.YoutubeComments;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Worker.Hosting
{
    public sealed class BackgroundJobSchedulerHostedService : IHostedService
    {
        private const string FacebookPublishingJobId = "pipeline:facebook-publish";
        private const string YoutubeCommentPublishingJobId = "pipeline:youtube-comment-publish";

        private readonly IRecurringJobManager _recurringJobs;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly IOptions<BackgroundJobOptions> _options;
        private readonly IOptions<FacebookOptions> _facebookOptions;
        private readonly IOptions<YoutubeCommentOptions> _youtubeCommentOptions;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<BackgroundJobSchedulerHostedService> _logger;

        public BackgroundJobSchedulerHostedService(
            IRecurringJobManager recurringJobs,
            IBackgroundJobClient backgroundJobs,
            IOptions<BackgroundJobOptions> options,
            IOptions<FacebookOptions> facebookOptions,
            IOptions<YoutubeCommentOptions> youtubeCommentOptions,
            IHostEnvironment environment,
            ILogger<BackgroundJobSchedulerHostedService> logger)
        {
            _recurringJobs = recurringJobs;
            _backgroundJobs = backgroundJobs;
            _options = options;
            _facebookOptions = facebookOptions;
            _youtubeCommentOptions = youtubeCommentOptions;
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

            if (_facebookOptions.Value.Enabled)
            {
                _recurringJobs.AddOrUpdate<FacebookPublishingJob>(
                    FacebookPublishingJobId,
                    job => job.ProcessOnceAsync(),
                    options.FacebookPublishCron);
            }
            else
            {
                _recurringJobs.RemoveIfExists(FacebookPublishingJobId);
            }

            if (_youtubeCommentOptions.Value.Enabled)
            {
                _recurringJobs.AddOrUpdate<YoutubeCommentPublishingJob>(
                    YoutubeCommentPublishingJobId,
                    job => job.ProcessOnceAsync(),
                    options.YoutubeCommentPublishCron);
            }
            else
            {
                _recurringJobs.RemoveIfExists(YoutubeCommentPublishingJobId);
            }

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

            if (_facebookOptions.Value.Enabled)
            {
                _backgroundJobs.ContinueJobWith<FacebookPublishingJob>(
                    summaryJobId,
                    job => job.ProcessOnceAsync());
            }

            if (_youtubeCommentOptions.Value.Enabled)
            {
                _backgroundJobs.ContinueJobWith<YoutubeCommentPublishingJob>(
                    summaryJobId,
                    job => job.ProcessOnceAsync());
            }

            _logger.LogInformation("Enqueued the development startup content pipeline.");
        }
    }
}
