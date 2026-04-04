using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models;
using ContentAggregator.Core.Services;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Publishes ready summaries and video links to the configured Facebook page,
    /// then records publish success or failure per video.
    /// </summary>
    public class FacebookService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FbPoster _fbPoster;
        private readonly ILogger<FacebookService> _logger;
        private readonly string _fbPageId;

        public FacebookService(IServiceProvider serviceProvider, FbPoster fbPoster, IConfiguration configuration, ILogger<FacebookService> logger)
        {
            _serviceProvider = serviceProvider;
            _fbPoster = fbPoster;
            _logger = logger;
            _fbPageId = configuration["FbPageId"]!;
        }

        [DisableConcurrentExecution(60 * 10)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var yTRepository = scope.ServiceProvider.GetRequiredService<IYoutubeContentRepository>();

                var youtubeContents = await yTRepository.GetYTContentsForFBPost();
                _logger.LogInformation("{Now}: DB query returned {Count} items ready to be posted on FB.", DateTimeOffset.UtcNow, youtubeContents.Count);

                if (!youtubeContents.Any())
                {
                    return;
                }

                foreach (var content in youtubeContents)
                {
                    var postUrl = $"https://www.youtube.com/watch?v={content.VideoId}";
                    var disclaimer = Constants.GetAiSummaryDisclaimer(content.SubtitleLanguage);
                    var message = content.VideoSummary + $"\n\n{disclaimer}";
                    var publishResult = await _fbPoster.SharePost(_fbPageId, postUrl, message, stoppingToken);

                    if (!publishResult.Success)
                    {
                        content.LastProcessingError = publishResult.Message;
                        _logger.LogWarning("FB post failed for content ID {ContentId}. {Message}", content.Id, publishResult.Message);
                        continue;
                    }

                    content.FbPosted = true;
                    content.LastProcessingError = null;
                    _logger.LogInformation("{Now}: Posted on FB. Content ID: {ContentId}.", DateTimeOffset.UtcNow, content.Id);
                }

                await yTRepository.UpdateYTContentsRangeAsync(youtubeContents);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Service} threw an exception.", nameof(FacebookService));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
