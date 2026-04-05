using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models
{
    public sealed record CreateYoutubeVideoResult(
        YoutubeContent? YoutubeContent,
        string? ChannelId,
        bool NotFound,
        string? ErrorMessage)
    {
        public bool Success => YoutubeContent != null && !string.IsNullOrWhiteSpace(ChannelId);
    }
}
