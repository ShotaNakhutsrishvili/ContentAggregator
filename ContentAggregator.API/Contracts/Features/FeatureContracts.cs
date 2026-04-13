namespace ContentAggregator.API.Contracts.Features
{
    public sealed record FeatureRequest
    {
        public required string FirstNameEng { get; init; }
        public required string LastNameEng { get; init; }
        public required string FirstNameGeo { get; init; }
        public required string LastNameGeo { get; init; }
    }

    public sealed record FeatureResponse(
        int Id,
        string FirstNameEng,
        string LastNameEng,
        string FirstNameGeo,
        string LastNameGeo,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
