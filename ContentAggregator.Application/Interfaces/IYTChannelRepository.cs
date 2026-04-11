using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYTChannelRepository
    {
        Task<YTChannel?> GetChannelByIdAsync(string id, CancellationToken cancellationToken);
        Task<YTChannel?> GetChannelByUrlAsync(Uri url, CancellationToken cancellationToken);
        Task<IReadOnlyList<YTChannel>> GetAllChannelsForAdminAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<YTChannel>> GetActiveChannelsForDiscoveryAsync(CancellationToken cancellationToken);
        Task AddChannelAsync(YTChannel channel, CancellationToken cancellationToken);
        Task<bool> UpdateChannelAsync(YTChannel channel, CancellationToken cancellationToken);
        Task<bool> DeleteChannelAsync(string id, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
