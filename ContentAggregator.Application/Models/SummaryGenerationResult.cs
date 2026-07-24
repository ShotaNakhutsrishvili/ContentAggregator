namespace ContentAggregator.Application.Models
{
    public sealed record SummaryGenerationResult(
        string Participants,
        string VideoSummary,
        IReadOnlyList<GeneratedContentSection> Sections,
        string GeneratorModel,
        string PromptVersion);
}
