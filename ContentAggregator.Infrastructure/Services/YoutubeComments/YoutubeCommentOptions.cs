namespace ContentAggregator.Infrastructure.Services.YoutubeComments
{
    public sealed class YoutubeCommentOptions
    {
        public const string SectionName = "YoutubeComment";

        public string OAuthAccessToken { get; set; } = string.Empty;
    }
}
