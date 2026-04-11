namespace ContentAggregator.Infrastructure.Services.Youtube.Transport
{
    internal sealed class YoutubeSearchResponse
    {
        public List<YoutubeSearchItem>? Items { get; set; }
    }

    internal sealed class YoutubeSearchItem
    {
        public YoutubeSearchItemId? Id { get; set; }
        public YoutubeSearchSnippet? Snippet { get; set; }
    }

    internal sealed class YoutubeSearchItemId
    {
        public string? VideoId { get; set; }
    }

    internal sealed class YoutubeSearchSnippet
    {
        public DateTimeOffset PublishedAt { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ChannelId { get; set; }
    }

    internal sealed class YoutubeChannelsResponse
    {
        public List<YoutubeChannelItem>? Items { get; set; }
    }

    internal sealed class YoutubeChannelItem
    {
        public YoutubeChannelSnippet? Snippet { get; set; }
    }

    internal sealed class YoutubeChannelSnippet
    {
        public string? CustomUrl { get; set; }
    }

    internal sealed class YoutubeVideosResponse
    {
        public List<YoutubeVideoItem>? Items { get; set; }
    }

    internal sealed class YoutubeVideoItem
    {
        public string? Id { get; set; }
        public YoutubeVideoContentDetails? ContentDetails { get; set; }
        public YoutubeVideoSnippet? Snippet { get; set; }
    }

    internal sealed class YoutubeVideoContentDetails
    {
        public string? Duration { get; set; }
    }

    internal sealed class YoutubeVideoSnippet
    {
        public DateTimeOffset PublishedAt { get; set; }
        public string? Title { get; set; }
        public string? ChannelId { get; set; }
        public string? ChannelTitle { get; set; }
    }
}
