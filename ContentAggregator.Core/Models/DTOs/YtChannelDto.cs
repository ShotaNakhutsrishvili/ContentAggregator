using ContentAggregator.Core.Entities;

namespace ContentAggregator.Core.Models.DTOs
{
    public class YtChannelDto
    {
        public required string ChannelSuffix { get; set; }
        public ChannelActivityLevel ActivityLevel { get; set; }
        public string? ChannelTitle { get; set; }
        public string? TitleKeywords { get; set; }
    }
}
