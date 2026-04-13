using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeVideoResult(
        YoutubeContent? Video,
        YTChannel? Channel,
        bool NotFound,
        string? ErrorMessage)
    {
        public bool Success => Video != null && Channel != null;
    }
}
