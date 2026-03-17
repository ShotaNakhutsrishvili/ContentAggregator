using ContentAggregator.Core.Models;
using ContentAggregator.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FbPostsController : ControllerBase
    {
        private readonly FbPoster _fbPoster;

        public FbPostsController(FbPoster fbPoster)
        {
            _fbPoster = fbPoster;
        }

        // POST: api/FbPosts
        [HttpPost]
        public async Task<IActionResult> SharePost(Post post)
        {
            try
            {
                var result = await _fbPoster.SharePost(post.PageId, post.Url?.ToString(), post.CustomText);
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
