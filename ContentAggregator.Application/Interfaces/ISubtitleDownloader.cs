using ContentAggregator.Application.Models;

namespace ContentAggregator.Application.Interfaces
{
    public interface ISubtitleDownloader
    {
        Task<DownloadedSubtitle?> DownloadAsync(string videoId, CancellationToken cancellationToken);
    }
}
