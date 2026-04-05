using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models
{
    public sealed record YoutubeContentListItem(
        int Id,
        string VideoId,
        string VideoTitle,
        string ChannelId,
        string? ChannelName,
        DateTimeOffset VideoPublishedAt,
        TimeSpan VideoLength,
        SubtitleLanguage SubtitleLanguage,
        string? Summary,
        bool SubtitlesReady,
        bool SummaryReady,
        bool FbPosted,
        bool YoutubeCommentReady,
        bool YoutubeCommentPosted,
        string? YoutubeCommentId,
        string? LastProcessingError);
}
