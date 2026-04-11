namespace ContentAggregator.Application.Models.Features
{
    public sealed record UpdateFeatureRequest
    {
        public required string FirstNameEng { get; init; }
        public required string LastNameEng { get; init; }
        public required string FirstNameGeo { get; init; }
        public required string LastNameGeo { get; init; }
    }
}
