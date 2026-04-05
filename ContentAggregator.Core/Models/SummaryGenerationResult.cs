namespace ContentAggregator.Core.Models
{
    public sealed record SummaryGenerationResult(
        string Participants,
        string VideoSummary,
        string YoutubeCommentText);
}
