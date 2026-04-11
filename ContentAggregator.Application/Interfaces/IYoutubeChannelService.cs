using ContentAggregator.Application.Models.Youtube;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeChannelService
    {
        Task<IReadOnlyList<YoutubeChannelListItemResponse>> GetAllAsync(CancellationToken cancellationToken);
        Task<YoutubeChannelDetailResponse?> GetByIdAsync(string id, CancellationToken cancellationToken);
        Task<YoutubeChannelDetailResponse?> UpdateAsync(
            string id,
            UpdateYoutubeChannelRequest request,
            CancellationToken cancellationToken);
        Task<CreateYoutubeChannelResult> CreateAsync(
            CreateYoutubeChannelRequest request,
            CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
        Task<CreateYoutubeVideoResult> CreateVideoAsync(
            CreateYoutubeVideoRequest request,
            CancellationToken cancellationToken);
    }
}
