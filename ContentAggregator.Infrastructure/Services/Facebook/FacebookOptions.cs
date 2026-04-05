namespace ContentAggregator.Infrastructure.Services.Facebook
{
    public sealed class FacebookOptions
    {
        public const string SectionName = "Facebook";

        public string AccessToken { get; set; } = string.Empty;

        public string PageId { get; set; } = string.Empty;
    }
}
