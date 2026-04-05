namespace ContentAggregator.Application.Models
{
    public sealed record YoutubeChannelVideoSearchMatch(
        string VideoId,
        string Title,
        string Description,
        DateTimeOffset PublishedAt);
}
