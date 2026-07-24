namespace ContentAggregator.Application.Models
{
    public sealed record GeneratedContentSection(
        int StartSeconds,
        int? EndSeconds,
        string Heading,
        string? Summary);
}
