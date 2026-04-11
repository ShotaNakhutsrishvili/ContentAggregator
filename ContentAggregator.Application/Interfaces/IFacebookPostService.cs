using ContentAggregator.Application.Models.Facebook;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFacebookPostService
    {
        Task<PublishFacebookPostResult> SharePostAsync(
            PublishFacebookPostRequest request,
            CancellationToken cancellationToken);
    }
}
