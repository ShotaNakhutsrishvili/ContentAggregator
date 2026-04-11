namespace ContentAggregator.Application.Models.Facebook
{
    public sealed record FacebookPostResponse(
        string Message,
        string? PostId,
        string PageId,
        string? Url);
}
