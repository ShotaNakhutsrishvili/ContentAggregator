using ContentAggregator.Core.Entities;
using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeContentRepository
    {
        Task<YoutubeContent?> GetYTContentsByIdAsync(int id);
        Task<List<YoutubeContent>> GetYTContentsNeedingRefetch();
        Task<List<YoutubeContent>> GetYTContentsWithoutSubtitles();
        Task<List<YoutubeContent>> GetYTContentsWithoutSummaries();
        Task<List<YoutubeContent>> GetYTContentsForFBPost();
        Task<List<YoutubeContent>> GetYTContentsForYoutubeCommentPost();
        Task AddYTContentFeature(YoutubeContentFeature contentFeature);
        Task AddYTContents(List<YoutubeContent> contents);
        Task UpdateYTContentsAsync(YoutubeContent yTContent);
        Task UpdateYTContentsRangeAsync(List<YoutubeContent> yTContents);
        Task<bool> DeleteYTContentAsync(int id, CancellationToken cancellationToken);
        Task<PagedYoutubeContentsResult> GetPagedAsync(int page, int pageSize, string? channelId, CancellationToken cancellationToken);
    }
}
