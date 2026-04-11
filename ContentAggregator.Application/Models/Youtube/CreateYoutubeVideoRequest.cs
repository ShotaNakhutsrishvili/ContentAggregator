namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeVideoRequest
    {
        public required Uri VideoUrl { get; init; }
        public string? ChannelSuffix { get; init; }
    }
}
