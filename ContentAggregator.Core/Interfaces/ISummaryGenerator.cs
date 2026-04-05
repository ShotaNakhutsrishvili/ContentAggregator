using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models;

namespace ContentAggregator.Core.Interfaces
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
