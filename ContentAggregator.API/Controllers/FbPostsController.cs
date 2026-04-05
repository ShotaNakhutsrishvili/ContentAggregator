using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Models;
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

        // POST: api/FbPosts
        [HttpPost]
        public async Task<IActionResult> SharePost(Post post)
        {
            try
            {
                var result = await _facebookPublisher.SharePostAsync(
                    post.PageId,
                    post.Url?.ToString(),
                    post.CustomText);
                if (!result.Success)
                {
                    return BadRequest(new { result.Message });
                }

                return Ok(new { result.Message, result.PostId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
