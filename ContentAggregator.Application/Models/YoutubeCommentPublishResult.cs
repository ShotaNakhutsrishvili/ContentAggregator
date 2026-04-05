namespace ContentAggregator.Application.Models
{
    public sealed record YoutubeCommentPublishResult(
        bool Success,
        string Message,
        string? CommentId);
}
