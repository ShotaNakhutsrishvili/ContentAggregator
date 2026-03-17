using System.ComponentModel.DataAnnotations.Schema;

namespace ContentAggregator.Core.Entities
{
    public class YoutubeContent
    {
        public int Id { get; set; }
        public required string VideoId { get; set; }
        public required string VideoTitle { get; set; }
        public required string ChannelId { get; set; }
        public TimeSpan VideoLength { get; set; }
        public DateTimeOffset VideoPublishedAt { get; set; }
        public bool NotRelevant { get; set; } = false; // Longer than required threshold but not political/social/etc.
        public string? SubtitlesEngSRT { get; set; }
        public string? SubtitlesFiltered { get; set; }
        public string? VideoSummaryEng { get; set; }
        public string? VideoSummaryGeo { get; set; }
        public string? AdditionalComments { get; set; }
        public bool FbPosted { get; set; } = false;
        public bool YoutubeCommentPosted { get; set; } = false;
        public string? YoutubeCommentId { get; set; }
        public DateTimeOffset? YoutubeCommentPostedAt { get; set; }
        public string? LastProcessingError { get; set; }
        public bool NeedsRefetch {  get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Now: e.g., 2023-10-05T14:30:00+02:00 ---- UtcNow: e.g., 2023-10-05T12:30:00+00:00
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }

        [ForeignKey("ChannelId")]
        public YTChannel? YTChannel { get; set; }
        public ICollection<Feature> Features { get; set; } = [];
        public ICollection<YoutubeContentFeature> YoutubeContentFeatures { get; set; } = [];
    }
}
