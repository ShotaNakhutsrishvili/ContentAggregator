using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record YoutubeChannelListItemResponse(
        string Id,
        string Name,
        string Url,
        string ChannelSuffix,
        ChannelActivityLevel ActivityLevel,
        DateTimeOffset? LastPublishedAt,
        string? TitleKeywords,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
