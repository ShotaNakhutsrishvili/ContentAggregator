using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Support;
using Microsoft.Extensions.Logging;

namespace ContentAggregator.Application.Services.Facebook
{
    public sealed class FacebookPublishingWorkflow : IFacebookPublishingWorkflow
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly IFacebookPublisher _facebookPublisher;
        private readonly ILogger<FacebookPublishingWorkflow> _logger;

        public FacebookPublishingWorkflow(
            IYoutubeContentRepository youtubeContentRepository,
            IFacebookPublisher facebookPublisher,
            ILogger<FacebookPublishingWorkflow> logger)
        {
            _youtubeContentRepository = youtubeContentRepository;
            _facebookPublisher = facebookPublisher;
            _logger = logger;
        }

        public async Task ProcessOnceAsync(CancellationToken cancellationToken)
        {
            if (!_facebookPublisher.IsConfigured)
            {
                _logger.LogWarning("Skipping Facebook publishing job because Facebook access token is not configured.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_facebookPublisher.DefaultPageId))
            {
                _logger.LogWarning("Skipping Facebook publishing job because FbPageId is not configured.");
                return;
            }

            try
            {
                var youtubeContents = await _youtubeContentRepository.GetYTContentsForFBPost();
                _logger.LogInformation(
                    "{Now}: DB query returned {Count} items ready to be posted on FB.",
                    DateTimeOffset.UtcNow,
                    youtubeContents.Count);

                if (!youtubeContents.Any())
                {
                    return;
                }

                foreach (var content in youtubeContents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var postUrl = $"https://www.youtube.com/watch?v={content.VideoId}";
                    var disclaimer = AiSummaryDisclaimer.GetText(content.SubtitleLanguage);
                    var message = content.VideoSummary + $"\n\n{disclaimer}";
                    var publishResult = await _facebookPublisher.SharePostAsync(
                        _facebookPublisher.DefaultPageId!,
                        postUrl,
                        message,
                        cancellationToken);

                    if (!publishResult.Success)
                    {
                        content.LastProcessingError = publishResult.Message;
                        _logger.LogWarning(
                            "FB post failed for content ID {ContentId}. {Message}",
                            content.Id,
                            publishResult.Message);
                        continue;
                    }

                    content.FbPosted = true;
                    content.LastProcessingError = null;
                    _logger.LogInformation(
                        "{Now}: Posted on FB. Content ID: {ContentId}.",
                        DateTimeOffset.UtcNow,
                        content.Id);
                }

                await _youtubeContentRepository.UpdateYTContentsRangeAsync(youtubeContents);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Workflow} was canceled.", nameof(FacebookPublishingWorkflow));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Workflow} threw an exception.", nameof(FacebookPublishingWorkflow));
            }
        }
    }
}
