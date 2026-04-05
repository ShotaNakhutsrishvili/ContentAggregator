using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YoutubeContentsController : ControllerBase
    {
        private readonly IYoutubeContentQueryService _youtubeContentQueryService;

        public YoutubeContentsController(IYoutubeContentQueryService youtubeContentQueryService)
        {
            _youtubeContentQueryService = youtubeContentQueryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedYoutubeContentsResult>> GetYoutubeContents(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? channelId = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _youtubeContentQueryService.GetPagedAsync(page, pageSize, channelId, cancellationToken);
            return Ok(result);
        }
    }
}
