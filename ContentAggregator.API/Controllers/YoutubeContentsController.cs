using ContentAggregator.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YoutubeContentsController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public YoutubeContentsController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedYoutubeContentResponse>> GetYoutubeContents(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? channelId = null,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.YoutubeContents
                .AsNoTracking()
                .Include(x => x.YTChannel)
                .Where(x => !x.NotRelevant);

            if (!string.IsNullOrWhiteSpace(channelId))
            {
                query = query.Where(x => x.ChannelId == channelId);
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.VideoPublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new YoutubeContentListItem(
                    x.Id,
                    x.VideoId,
                    x.VideoTitle,
                    x.ChannelId,
                    x.YTChannel != null ? x.YTChannel.Name : null,
                    x.VideoPublishedAt,
                    x.VideoLength,
                    x.VideoSummaryGeo,
                    x.SubtitlesFiltered != null,
                    x.VideoSummaryGeo != null,
                    x.FbPosted,
                    x.YoutubeCommentPosted,
                    x.YoutubeCommentId,
                    x.LastProcessingError))
                .ToListAsync(cancellationToken);

            return Ok(new PagedYoutubeContentResponse(total, page, pageSize, items));
        }
    }

    public sealed record YoutubeContentListItem(
        int Id,
        string VideoId,
        string VideoTitle,
        string ChannelId,
        string? ChannelName,
        DateTimeOffset VideoPublishedAt,
        TimeSpan VideoLength,
        string? Summary,
        bool SubtitlesReady,
        bool SummaryReady,
        bool FbPosted,
        bool YoutubeCommentPosted,
        string? YoutubeCommentId,
        string? LastProcessingError);

    public sealed record PagedYoutubeContentResponse(
        int Total,
        int Page,
        int PageSize,
        IReadOnlyList<YoutubeContentListItem> Items);
}
