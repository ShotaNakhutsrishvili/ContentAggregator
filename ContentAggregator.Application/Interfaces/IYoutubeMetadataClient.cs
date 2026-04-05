using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeMetadataClient
    {
        Task<IReadOnlyList<YoutubeChannelSearchMatch>> SearchChannelsAsync(string query, CancellationToken cancellationToken);
        Task<IReadOnlyList<YoutubeChannelVideoSearchMatch>> SearchChannelVideosAsync(
            string channelId,
            DateTimeOffset? publishedAfter,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<YoutubeVideoMetadata>> GetVideosAsync(
            IEnumerable<string> videoIds,
            CancellationToken cancellationToken);
        Task<YoutubeVideoMetadata?> GetVideoAsync(string videoId, CancellationToken cancellationToken);
        Task<string?> GetChannelCustomUrlAsync(string channelId, CancellationToken cancellationToken);
    }
}
