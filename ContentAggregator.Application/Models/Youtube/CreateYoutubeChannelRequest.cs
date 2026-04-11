using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeChannelRequest
    {
        public required string ChannelSuffix { get; init; }
        public ChannelActivityLevel ActivityLevel { get; init; }
        public string? ChannelTitle { get; init; }
        public string? TitleKeywords { get; init; }
    }
}
