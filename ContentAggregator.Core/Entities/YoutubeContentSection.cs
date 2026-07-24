namespace ContentAggregator.Core.Entities
{
    public class YoutubeContentSection
    {
        private YoutubeContentSection()
        {
        }

        internal YoutubeContentSection(
            int ordinal,
            int startSeconds,
            int? endSeconds,
            string heading,
            string? summary,
            ContentSectionSource source,
            decimal? confidence)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
            ArgumentOutOfRangeException.ThrowIfNegative(startSeconds);
            ArgumentException.ThrowIfNullOrWhiteSpace(heading);

            if (endSeconds.HasValue && endSeconds.Value <= startSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endSeconds),
                    "Section end must be greater than its start.");
            }

            if (confidence is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confidence),
                    "Section confidence must be between 0 and 1.");
            }

            Ordinal = ordinal;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            Heading = heading.Trim();
            Summary = summary?.Trim();
            Source = source;
            Confidence = confidence;
        }

        public int Id { get; private set; }

        public int YoutubeContentRevisionId { get; private set; }

        public int Ordinal { get; private set; }

        public int StartSeconds { get; private set; }

        public int? EndSeconds { get; private set; }

        public string Heading { get; private set; } = string.Empty;

        public string? Summary { get; private set; }

        public ContentSectionSource Source { get; private set; } = ContentSectionSource.Machine;

        public decimal? Confidence { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

        public YoutubeContentRevision? YoutubeContentRevision { get; private set; }
    }
}
