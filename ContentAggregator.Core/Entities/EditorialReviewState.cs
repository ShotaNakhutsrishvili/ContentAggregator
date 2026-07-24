namespace ContentAggregator.Core.Entities
{
    public enum EditorialReviewState : byte
    {
        Draft = 0,
        ReadyForReview = 1,
        ChangesRequested = 2,
        Approved = 3,
        Rejected = 4
    }
}
