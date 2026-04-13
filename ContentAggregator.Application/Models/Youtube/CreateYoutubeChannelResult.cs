using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeChannelResult(
        YTChannel? Channel,
        string? ErrorMessage)
    {
        public bool Success => Channel != null;
    }
}
