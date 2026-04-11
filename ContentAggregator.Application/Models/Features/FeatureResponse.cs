namespace ContentAggregator.Application.Models.Features
{
    public sealed record FeatureResponse(
        int Id,
        string FirstNameEng,
        string LastNameEng,
        string FirstNameGeo,
        string LastNameGeo,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
