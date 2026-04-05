namespace ContentAggregator.Infrastructure.Services.Youtube
{
    public sealed class YoutubeApiOptions
    {
        public const string SectionName = "YoutubeApi";

        public string ApiKey { get; set; } = string.Empty;
    }
}
