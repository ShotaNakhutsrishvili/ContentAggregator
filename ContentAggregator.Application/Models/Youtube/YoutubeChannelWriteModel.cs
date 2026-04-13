using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record YoutubeChannelWriteModel(
        string ChannelSuffix,
        ChannelActivityLevel ActivityLevel,
        string? ChannelTitle,
        string? TitleKeywords);
}
