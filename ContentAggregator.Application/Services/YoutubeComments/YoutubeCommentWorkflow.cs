using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Support;
using Microsoft.Extensions.Logging;

namespace ContentAggregator.Application.Services.YoutubeComments
{
    public sealed class YoutubeCommentWorkflow : IYoutubeCommentWorkflow
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly IYoutubeCommentPublisher _youtubeCommentPublisher;
        private readonly ILogger<YoutubeCommentWorkflow> _logger;

        public YoutubeCommentWorkflow(
            IYoutubeContentRepository youtubeContentRepository,
            IYoutubeCommentPublisher youtubeCommentPublisher,
            ILogger<YoutubeCommentWorkflow> logger)
        {
            _youtubeContentRepository = youtubeContentRepository;
            _youtubeCommentPublisher = youtubeCommentPublisher;
            _logger = logger;
        }

        public async Task ProcessOnceAsync(CancellationToken cancellationToken)
        {
            if (!_youtubeCommentPublisher.IsConfigured)
            {
                _logger.LogWarning("Skipping YouTube comment job because YoutubeOAuthAccessToken is not configured.");
                return;
            }

            try
            {
                var youtubeContents = await _youtubeContentRepository.GetYTContentsForYoutubeCommentPost();
                if (!youtubeContents.Any())
                {
                    return;
                }

                foreach (var content in youtubeContents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var commentText = BuildCommentText(
                        content.YoutubeCommentText ?? content.VideoSummary ?? string.Empty,
                        content.SubtitleLanguage);
                    var publishResult = await _youtubeCommentPublisher.PublishAsync(
                        content.VideoId,
                        commentText,
                        cancellationToken);

                    if (!publishResult.Success)
                    {
                        content.LastProcessingError = publishResult.Message;
                        _logger.LogWarning(
                            "YouTube comment failed for content ID {ContentId}. {Message}",
                            content.Id,
                            publishResult.Message);
                        continue;
                    }

                    content.YoutubeCommentPosted = true;
                    content.YoutubeCommentId = publishResult.CommentId;
                    content.YoutubeCommentPostedAt = DateTimeOffset.UtcNow;
                    content.LastProcessingError = null;
                }

                await _youtubeContentRepository.UpdateYTContentsRangeAsync(youtubeContents);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Workflow} was canceled.", nameof(YoutubeCommentWorkflow));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Workflow} threw an exception.", nameof(YoutubeCommentWorkflow));
            }
        }

        private static string BuildCommentText(string summary, Core.Entities.SubtitleLanguage subtitleLanguage)
        {
            var disclaimer = AiSummaryDisclaimer.GetText(subtitleLanguage);
            var result = summary + Environment.NewLine + Environment.NewLine + disclaimer;
            const int maxLen = 900;
            if (result.Length <= maxLen)
            {
                return result;
            }

            return result[..(maxLen - 3)] + "...";
        }
    }
}
