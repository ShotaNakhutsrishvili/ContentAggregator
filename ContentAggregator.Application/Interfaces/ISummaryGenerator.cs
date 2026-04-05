using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface ISummaryGenerator
    {
        Task<SummaryGenerationResult> GenerateAsync(
            string filteredTranscript,
            string? originalSrt,
            SubtitleLanguage subtitleLanguage,
            CancellationToken cancellationToken);
    }
}
