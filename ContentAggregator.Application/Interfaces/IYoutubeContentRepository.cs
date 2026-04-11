using ContentAggregator.Core.Entities;
using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeContentRepository
    {
        Task<YoutubeContent?> GetYTContentsByIdAsync(int id, CancellationToken cancellationToken);
        Task<List<YoutubeContent>> GetYTContentsNeedingRefetch(CancellationToken cancellationToken);
        Task<List<YoutubeContent>> GetYTContentsWithoutSubtitles(CancellationToken cancellationToken);
        Task<List<YoutubeContent>> GetYTContentsWithoutSummaries(CancellationToken cancellationToken);
        Task<List<YoutubeContent>> GetYTContentsForFBPost(CancellationToken cancellationToken);
        Task<List<YoutubeContent>> GetYTContentsForYoutubeCommentPost(CancellationToken cancellationToken);
        Task AddYTContentFeature(YoutubeContentFeature contentFeature, CancellationToken cancellationToken);
        Task AddYTContents(List<YoutubeContent> contents, CancellationToken cancellationToken);
        Task UpdateYTContentsAsync(YoutubeContent yTContent, CancellationToken cancellationToken);
        Task UpdateYTContentsRangeAsync(List<YoutubeContent> yTContents, CancellationToken cancellationToken);
        Task<bool> DeleteYTContentAsync(int id, CancellationToken cancellationToken);
        Task<PagedYoutubeContentsResult> GetPagedAsync(int page, int pageSize, string? channelId, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
