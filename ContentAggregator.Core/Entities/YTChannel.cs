namespace ContentAggregator.Core.Entities
{
    public class YTChannel
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required Uri Url { get; set; }
        public ChannelActivityLevel ActivityLevel { get; set; } = ChannelActivityLevel.Disabled;
        public DateTimeOffset? LastPublishedAt { get; set; }
        public string? TitleKeywords { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }

        public ICollection<YoutubeContent> YoutubeContents { get; set; } = new List<YoutubeContent>();
    }
}
