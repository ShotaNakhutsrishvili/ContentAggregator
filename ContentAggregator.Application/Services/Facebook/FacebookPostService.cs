using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models.Facebook;

namespace ContentAggregator.Application.Services.Facebook
{
    public sealed class FacebookPostService : IFacebookPostService
    {
        private readonly IFacebookPublisher _facebookPublisher;

        public FacebookPostService(IFacebookPublisher facebookPublisher)
        {
            _facebookPublisher = facebookPublisher;
        }

        public async Task<PublishFacebookPostResult> SharePostAsync(
            PublishFacebookPostRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PageId))
            {
                return new PublishFacebookPostResult(null, "PageId is required.");
            }

            if (request.Url == null && string.IsNullOrWhiteSpace(request.CustomText))
            {
                return new PublishFacebookPostResult(null, "Either url or customText must be provided.");
            }

            var result = await _facebookPublisher.SharePostAsync(
                request.PageId,
                request.Url?.ToString(),
                request.CustomText,
                cancellationToken);

            if (!result.Success)
            {
                return new PublishFacebookPostResult(null, result.Message);
            }

            return new PublishFacebookPostResult(
                new FacebookPostResponse(
                    result.Message,
                    result.PostId,
                    request.PageId,
                    request.Url?.ToString()),
                null);
        }
    }
}
