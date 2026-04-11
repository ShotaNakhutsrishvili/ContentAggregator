using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models.Facebook;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FbPostsController : ControllerBase
    {
        private readonly IFacebookPostService _facebookPostService;

        public FbPostsController(IFacebookPostService facebookPostService)
        {
            _facebookPostService = facebookPostService;
        }

        [HttpPost]
        public async Task<ActionResult<FacebookPostResponse>> SharePost(
            [FromBody] PublishFacebookPostRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _facebookPostService.SharePostAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Ok(result.Post);
        }
    }
}
