namespace ContentAggregator.Application.Models.Facebook
{
    public sealed record PublishFacebookPostResult(
        FacebookPostResponse? Post,
        string? ErrorMessage)
    {
        public bool Success => Post != null;
    }
}
