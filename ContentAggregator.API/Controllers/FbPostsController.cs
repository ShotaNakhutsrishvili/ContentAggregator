using ContentAggregator.API.Contracts.Facebook;
using ContentAggregator.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FbPostsController : ControllerBase
    {
        private readonly IFacebookPublisher _facebookPublisher;

        public FbPostsController(IFacebookPublisher facebookPublisher)
        {
            _facebookPublisher = facebookPublisher;
        }

        [HttpPost]
        public async Task<ActionResult<FacebookPostResponse>> SharePost(
            [FromBody] PublishFacebookPostRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PageId))
            {
                return BadRequest("PageId is required.");
            }

            if (request.Url == null && string.IsNullOrWhiteSpace(request.CustomText))
            {
                return BadRequest("Either url or customText must be provided.");
            }

            var result = await _facebookPublisher.SharePostAsync(
                request.PageId,
                request.Url?.ToString(),
                request.CustomText,
                cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new FacebookPostResponse(
                result.Message,
                result.PostId,
                request.PageId,
                request.Url?.ToString()));
        }
    }
}
