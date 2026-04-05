using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Services.YoutubeContents
{
    public sealed class YoutubeContentQueryService : IYoutubeContentQueryService
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;

        public YoutubeContentQueryService(IYoutubeContentRepository youtubeContentRepository)
        {
            _youtubeContentRepository = youtubeContentRepository;
        }

        public async Task<PagedYoutubeContentsResult> GetPagedAsync(
            int page,
            int pageSize,
            string? channelId,
            CancellationToken cancellationToken)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            return await _youtubeContentRepository.GetPagedAsync(page, pageSize, channelId, cancellationToken);
        }
    }
}
