using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFacebookPublisher
    {
        bool IsConfigured { get; }

        string? DefaultPageId { get; }

        Task<FacebookPublishResult> SharePostAsync(
            string pageId,
            string? postUrl,
            string? message,
            CancellationToken cancellationToken = default);
    }
}
