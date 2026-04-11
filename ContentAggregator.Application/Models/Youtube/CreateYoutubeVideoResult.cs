namespace ContentAggregator.Application.Models.Youtube
{
    public sealed record CreateYoutubeVideoResult(
        YoutubeVideoResponse? Video,
        bool NotFound,
        string? ErrorMessage)
    {
        public bool Success => Video != null;
    }
}
