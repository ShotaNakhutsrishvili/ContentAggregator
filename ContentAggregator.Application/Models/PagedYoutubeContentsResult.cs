namespace ContentAggregator.Application.Models
{
    public sealed record PagedYoutubeContentsResult(
        int Total,
        int Page,
        int PageSize,
        IReadOnlyList<YoutubeContentListItem> Items);
}
