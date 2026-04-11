using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
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
