using ContentAggregator.Core.Entities;

namespace ContentAggregator.API.Contracts.Youtube
{
    public sealed record YoutubeChannelRequest
    {
        public required string ChannelSuffix { get; init; }
        public ChannelActivityLevel ActivityLevel { get; init; }
        public string? ChannelTitle { get; init; }
        public string? TitleKeywords { get; init; }
    }

    public sealed record CreateYoutubeVideoRequest
    {
        public required Uri VideoUrl { get; init; }
        public string? ChannelSuffix { get; init; }
    }

    public sealed record YoutubeChannelResponse(
        string Id,
        string Name,
        string? Description,
        string Url,
        string ChannelSuffix,
        ChannelActivityLevel ActivityLevel,
        DateTimeOffset? LastPublishedAt,
        string? TitleKeywords,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record YoutubeVideoResponse(
        int Id,
        string VideoId,
        string VideoTitle,
        string VideoUrl,
        TimeSpan VideoLength,
        DateTimeOffset VideoPublishedAt,
        DateTimeOffset CreatedAt,
        string ChannelId,
        string ChannelName,
        string ChannelUrl,
        string ChannelSuffix,
        ChannelActivityLevel ChannelActivityLevel);
}
