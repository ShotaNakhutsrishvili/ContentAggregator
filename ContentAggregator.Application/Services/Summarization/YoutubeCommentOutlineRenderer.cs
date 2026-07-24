using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Services.Summarization
{
    internal static class YoutubeCommentOutlineRenderer
    {
        public static string Render(IReadOnlyCollection<YoutubeContentSection> sections)
        {
            return string.Join(
                Environment.NewLine,
                sections
                    .OrderBy(section => section.Ordinal)
                    .Select(section => $"{FormatTimestamp(section.StartSeconds)} - {section.Heading}"));
        }

        private static string FormatTimestamp(int totalSeconds)
        {
            var timestamp = TimeSpan.FromSeconds(totalSeconds);
            return timestamp.TotalHours >= 1
                ? $"{(int)timestamp.TotalHours}:{timestamp.Minutes:00}:{timestamp.Seconds:00}"
                : $"{timestamp.Minutes:00}:{timestamp.Seconds:00}";
        }
    }
}
