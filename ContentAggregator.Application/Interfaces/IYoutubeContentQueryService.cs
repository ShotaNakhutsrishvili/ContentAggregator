using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeContentQueryService
    {
        Task<PagedYoutubeContentsResult> GetPagedAsync(
            int page,
            int pageSize,
            string? channelId,
            CancellationToken cancellationToken);
    }
}
