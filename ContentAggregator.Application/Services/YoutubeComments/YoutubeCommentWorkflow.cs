using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Services.Summarization;
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
            if (!_youtubeCommentPublisher.IsEnabled)
            {
                _logger.LogInformation("Skipping YouTube comment job because publishing is disabled.");
                return;
            }

            if (!_youtubeCommentPublisher.IsConfigured)
            {
                _logger.LogWarning("Skipping YouTube comment job because YoutubeComment:OAuthAccessToken is not configured.");
                return;
            }

            try
            {
                var youtubeContents = await _youtubeContentRepository.GetYTContentsForYoutubeCommentPost(cancellationToken);
                if (!youtubeContents.Any())
                {
                    return;
                }

                foreach (var content in youtubeContents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var approvedRevision = content.Publication?.PublishedRevision;
                    if (approvedRevision == null)
                    {
                        content.LastProcessingError = "Published YouTube content has no approved revision.";
                        continue;
                    }

                    var outline = YoutubeCommentOutlineRenderer.Render(approvedRevision.Sections);
                    if (!TryBuildCommentText(
                            outline,
                            approvedRevision.Language,
                            out var commentText,
                            out var validationError))
                    {
                        content.LastProcessingError = validationError;
                        continue;
                    }

                    var publishResult = await _youtubeCommentPublisher.PublishAsync(
                        content.VideoId,
                        commentText!,
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

                await _youtubeContentRepository.UpdateYTContentsRangeAsync(youtubeContents, cancellationToken);
                await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
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

        private static bool TryBuildCommentText(
            string outline,
            Core.Entities.SubtitleLanguage subtitleLanguage,
            out string? commentText,
            out string? validationError)
        {
            var disclaimer = AiSummaryDisclaimer.GetText(subtitleLanguage);
            commentText = outline + Environment.NewLine + Environment.NewLine + disclaimer;

            const int maxCommentLength = 10_000;
            if (commentText.Length <= maxCommentLength)
            {
                validationError = null;
                return true;
            }

            validationError =
                $"YouTube comment is {commentText.Length} characters; the maximum is {maxCommentLength}.";
            commentText = null;
            return false;
        }
    }
}
