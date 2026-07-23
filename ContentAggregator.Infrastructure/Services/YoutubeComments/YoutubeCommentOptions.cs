namespace ContentAggregator.Infrastructure.Services.YoutubeComments
{
    public sealed class YoutubeCommentOptions
    {
        public const string SectionName = "YoutubeComment";

        public bool Enabled { get; set; }

        public string OAuthAccessToken { get; set; } = string.Empty;
    }
}
