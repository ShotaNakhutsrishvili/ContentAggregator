namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeChannelResult(
        YoutubeChannelDetailResponse? Channel,
        string? ErrorMessage)
    {
        public bool Success => Channel != null;
    }
}
