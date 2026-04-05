namespace ContentAggregator.Application.Models
{
    public sealed record FacebookPublishResult(
        bool Success,
        string Message,
        string? PostId);
}
