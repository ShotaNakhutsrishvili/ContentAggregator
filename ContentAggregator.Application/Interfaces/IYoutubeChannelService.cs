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
            string channelSuffix,
            ChannelActivityLevel activityLevel,
            string? channelTitle,
            string? titleKeywords,
            CancellationToken cancellationToken);
        Task<CreateYoutubeChannelResult> CreateAsync(
            string channelSuffix,
            ChannelActivityLevel activityLevel,
            string? channelTitle,
            string? titleKeywords,
            CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
        Task<CreateYoutubeVideoResult> CreateVideoAsync(
            Uri videoUrl,
            string? channelSuffix,
            CancellationToken cancellationToken);
    }
}
