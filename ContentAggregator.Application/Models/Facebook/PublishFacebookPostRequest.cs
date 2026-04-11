namespace ContentAggregator.Application.Models.Facebook
{
    public sealed record PublishFacebookPostRequest
    {
        public required string PageId { get; init; }
        public Uri? Url { get; init; }
        public string? CustomText { get; init; }
    }
}
