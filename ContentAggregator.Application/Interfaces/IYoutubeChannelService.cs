using ContentAggregator.Application.Models.Youtube;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeChannelService
    {
        Task<IReadOnlyList<YTChannel>> GetAllAsync(CancellationToken cancellationToken);
        Task<YTChannel?> GetByIdAsync(string id, CancellationToken cancellationToken);
        Task<YTChannel?> UpdateAsync(
            string id,
            YoutubeChannelWriteModel model,
            CancellationToken cancellationToken);
        Task<CreateYoutubeChannelResult> CreateAsync(
            YoutubeChannelWriteModel model,
            CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
        Task<CreateYoutubeVideoResult> CreateVideoAsync(
            CreateYoutubeVideoInput input,
            CancellationToken cancellationToken);
    }
}
