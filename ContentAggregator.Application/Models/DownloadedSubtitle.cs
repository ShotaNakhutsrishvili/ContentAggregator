using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models
{
    public sealed record DownloadedSubtitle(
        string OriginalSrt,
        string FilteredText,
        SubtitleLanguage Language);
}
