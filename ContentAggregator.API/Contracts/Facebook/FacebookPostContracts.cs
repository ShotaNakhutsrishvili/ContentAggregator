namespace ContentAggregator.API.Contracts.Facebook
{
    public sealed record PublishFacebookPostRequest
    {
        public required string PageId { get; init; }
        public Uri? Url { get; init; }
        public string? CustomText { get; init; }
    }

    public sealed record FacebookPostResponse(
        string Message,
        string? PostId,
        string PageId,
        string? Url);
}
