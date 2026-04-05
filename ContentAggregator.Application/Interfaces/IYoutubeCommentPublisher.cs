using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeCommentPublisher
    {
        bool IsConfigured { get; }

        Task<YoutubeCommentPublishResult> PublishAsync(
            string videoId,
            string text,
            CancellationToken cancellationToken);
    }
}
