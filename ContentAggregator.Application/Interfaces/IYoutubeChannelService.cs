using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeChannelService
    {
        Task<IEnumerable<YTChannel>> GetAllAsync(CancellationToken cancellationToken);
        Task<YTChannel?> GetByIdAsync(string id, CancellationToken cancellationToken);
        Task<YTChannel?> UpdateAsync(string id, YtChannelDto yTChannelDto, CancellationToken cancellationToken);
        Task<CreateChannelResult> CreateAsync(YtChannelDto channelDto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
        Task<CreateYoutubeVideoResult> CreateVideoAsync(Uri videoUrl, string? channelSuffix, CancellationToken cancellationToken);
    }
}
