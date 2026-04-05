namespace ContentAggregator.Application.Models
{
    public sealed record YoutubeVideoMetadata(
        string VideoId,
        string Title,
        string ChannelId,
        string ChannelTitle,
        DateTimeOffset PublishedAt,
        TimeSpan VideoLength);
}
