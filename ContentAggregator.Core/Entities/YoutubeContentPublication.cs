namespace ContentAggregator.Core.Entities
{
    public class YoutubeContentPublication
    {
        private YoutubeContentPublication()
        {
        }

        public YoutubeContentPublication(int youtubeContentId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(youtubeContentId);
            YoutubeContentId = youtubeContentId;
        }

        public int YoutubeContentId { get; private set; }

        public int? PublishedRevisionId { get; private set; }

        public SitePublicationState State { get; private set; } = SitePublicationState.Unpublished;

        public DateTimeOffset? PublishedAt { get; private set; }

        public DateTimeOffset? WithdrawnAt { get; private set; }

        public string? WithdrawalReason { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? UpdatedAt { get; private set; }

        public uint ConcurrencyVersion { get; private set; }

        public YoutubeContent? YoutubeContent { get; private set; }

        public YoutubeContentRevision? PublishedRevision { get; private set; }

        public void Publish(YoutubeContentRevision revision)
        {
            ArgumentNullException.ThrowIfNull(revision);

            if (revision.YoutubeContentId != YoutubeContentId)
            {
                throw new InvalidOperationException("Cannot publish a revision for a different video.");
            }

            if (revision.Id <= 0)
            {
                throw new InvalidOperationException("A revision must be persisted before it can be published.");
            }

            if (revision.ReviewState != EditorialReviewState.Approved)
            {
                throw new InvalidOperationException("Only an approved revision can be published.");
            }

            var now = DateTimeOffset.UtcNow;
            PublishedRevisionId = revision.Id;
            PublishedRevision = revision;
            State = SitePublicationState.Published;
            PublishedAt = now;
            WithdrawnAt = null;
            WithdrawalReason = null;
            UpdatedAt = now;
        }

        public void Withdraw(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            if (State != SitePublicationState.Published)
            {
                throw new InvalidOperationException("Only published content can be withdrawn.");
            }

            State = SitePublicationState.Withdrawn;
            WithdrawnAt = DateTimeOffset.UtcNow;
            WithdrawalReason = reason.Trim();
            UpdatedAt = WithdrawnAt;
        }
    }
}
