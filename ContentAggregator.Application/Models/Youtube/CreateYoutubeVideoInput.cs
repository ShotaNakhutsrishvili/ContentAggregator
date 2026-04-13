namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeVideoInput(
        Uri VideoUrl,
        string? ChannelSuffix);
}
