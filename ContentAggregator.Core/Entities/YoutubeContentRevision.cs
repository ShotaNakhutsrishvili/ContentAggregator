namespace ContentAggregator.Core.Entities
{
    public class YoutubeContentRevision
    {
        private readonly List<YoutubeContentSection> _sections = [];

        private YoutubeContentRevision()
        {
        }

        public YoutubeContentRevision(
            int youtubeContentId,
            int version,
            SubtitleLanguage language,
            string summary,
            string transcriptChecksum,
            string? generatorModel,
            string? promptVersion)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(youtubeContentId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
            ArgumentException.ThrowIfNullOrWhiteSpace(transcriptChecksum);

            YoutubeContentId = youtubeContentId;
            Version = version;
            Language = language;
            Summary = summary?.Trim() ?? string.Empty;
            TranscriptChecksum = transcriptChecksum.Trim();
            GeneratorModel = generatorModel?.Trim();
            PromptVersion = promptVersion?.Trim();
        }

        public int Id { get; private set; }

        public int YoutubeContentId { get; private set; }

        public int Version { get; private set; }

        public SubtitleLanguage Language { get; private set; } = SubtitleLanguage.Unknown;

        public string Summary { get; private set; } = string.Empty;

        public string TranscriptChecksum { get; private set; } = string.Empty;

        public string? GeneratorModel { get; private set; }

        public string? PromptVersion { get; private set; }

        public EditorialReviewState ReviewState { get; private set; } = EditorialReviewState.Draft;

        public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? UpdatedAt { get; private set; }

        public DateTimeOffset? ReviewedAt { get; private set; }

        public string? ReviewedBy { get; private set; }

        public uint ConcurrencyVersion { get; private set; }

        public YoutubeContent? YoutubeContent { get; private set; }

        public YoutubeContentPublication? Publication { get; private set; }

        public IReadOnlyCollection<YoutubeContentSection> Sections => _sections;

        public void UpdateDraftSummary(string summary)
        {
            EnsureEditable();
            Summary = summary?.Trim() ?? string.Empty;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public YoutubeContentSection AddSection(
            int startSeconds,
            int? endSeconds,
            string heading,
            string? summary,
            ContentSectionSource source,
            decimal? confidence)
        {
            EnsureEditable();

            var previousSection = _sections.LastOrDefault();
            if (previousSection != null)
            {
                if (startSeconds <= previousSection.StartSeconds)
                {
                    throw new InvalidOperationException(
                        "Section start times must be strictly increasing.");
                }

                if (previousSection.EndSeconds.HasValue
                    && previousSection.EndSeconds.Value > startSeconds)
                {
                    throw new InvalidOperationException("Content sections cannot overlap.");
                }
            }

            var section = new YoutubeContentSection(
                _sections.Count,
                startSeconds,
                endSeconds,
                heading,
                summary,
                source,
                confidence);

            _sections.Add(section);
            UpdatedAt = DateTimeOffset.UtcNow;
            return section;
        }

        public void MarkReadyForReview(TimeSpan videoLength)
        {
            EnsureEditable();

            if (string.IsNullOrWhiteSpace(Summary))
            {
                throw new InvalidOperationException("A revision summary is required before review.");
            }

            if (_sections.Count == 0)
            {
                throw new InvalidOperationException("At least one content section is required before review.");
            }

            if (videoLength <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("A positive video duration is required before review.");
            }

            var videoLengthSeconds = (int)Math.Ceiling(videoLength.TotalSeconds);
            if (_sections.Any(section =>
                    section.StartSeconds >= videoLengthSeconds
                    || section.EndSeconds > videoLengthSeconds))
            {
                throw new InvalidOperationException("Content section timestamps must be within the video duration.");
            }

            ReviewState = EditorialReviewState.ReadyForReview;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void RequestChanges(string reviewedBy)
        {
            EnsureReviewer(reviewedBy);

            if (ReviewState != EditorialReviewState.ReadyForReview)
            {
                throw new InvalidOperationException("Only a revision ready for review can have changes requested.");
            }

            ReviewState = EditorialReviewState.ChangesRequested;
            ReviewedAt = DateTimeOffset.UtcNow;
            ReviewedBy = reviewedBy.Trim();
            UpdatedAt = ReviewedAt;
        }

        public void Approve(string reviewedBy)
        {
            EnsureReviewer(reviewedBy);

            if (ReviewState != EditorialReviewState.ReadyForReview)
            {
                throw new InvalidOperationException("Only a revision ready for review can be approved.");
            }

            ReviewState = EditorialReviewState.Approved;
            ReviewedAt = DateTimeOffset.UtcNow;
            ReviewedBy = reviewedBy.Trim();
            UpdatedAt = ReviewedAt;
        }

        public void Reject(string reviewedBy)
        {
            EnsureReviewer(reviewedBy);

            if (ReviewState != EditorialReviewState.ReadyForReview)
            {
                throw new InvalidOperationException("Only a revision ready for review can be rejected.");
            }

            ReviewState = EditorialReviewState.Rejected;
            ReviewedAt = DateTimeOffset.UtcNow;
            ReviewedBy = reviewedBy.Trim();
            UpdatedAt = ReviewedAt;
        }

        private void EnsureEditable()
        {
            if (ReviewState is not EditorialReviewState.Draft
                and not EditorialReviewState.ChangesRequested)
            {
                throw new InvalidOperationException(
                    $"Revision {Id} cannot be edited while it is in state {ReviewState}.");
            }
        }

        private static void EnsureReviewer(string reviewedBy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);
        }
    }
}
