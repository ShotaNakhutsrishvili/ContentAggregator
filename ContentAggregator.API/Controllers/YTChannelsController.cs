using ContentAggregator.API.Contracts.Youtube;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YTChannelsController : ControllerBase
    {
        private readonly IYoutubeChannelService _youtubeChannelService;

        public YTChannelsController(IYoutubeChannelService youtubeChannelService)
        {
            _youtubeChannelService = youtubeChannelService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<YoutubeChannelResponse>>> GetYTChannels(
            CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.GetAllAsync(cancellationToken);
            return Ok(result.Select(MapToChannelResponse).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<YoutubeChannelResponse>> GetYTChannel(
            string id,
            CancellationToken cancellationToken)
        {
            var channel = await _youtubeChannelService.GetByIdAsync(id, cancellationToken);
            if (channel == null)
            {
                return NotFound();
            }

            return Ok(MapToChannelResponse(channel));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<YoutubeChannelResponse>> PutYTChannel(
            string id,
            [FromHeader(Name = "Prefer")] string? preferHeader,
            [FromBody] YoutubeChannelRequest request,
            CancellationToken cancellationToken)
        {
            var existingChannel = await _youtubeChannelService.UpdateAsync(
                id,
                request.ChannelSuffix,
                request.ActivityLevel,
                request.ChannelTitle,
                request.TitleKeywords,
                cancellationToken);
            if (existingChannel == null)
            {
                return NotFound();
            }

            var wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;
            return wantsMinimalResponse ? NoContent() : Ok(MapToChannelResponse(existingChannel));
        }

        [HttpPost]
        public async Task<ActionResult<YoutubeChannelResponse>> PostYTChannel(
            [FromBody] YoutubeChannelRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.CreateAsync(
                request.ChannelSuffix,
                request.ActivityLevel,
                request.ChannelTitle,
                request.TitleKeywords,
                cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            var response = MapToChannelResponse(result.Channel!);
            return CreatedAtAction(nameof(GetYTChannel), new { id = response.Id }, response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteYTChannel(string id, CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.DeleteAsync(id, cancellationToken);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("videos")]
        public async Task<ActionResult<YoutubeVideoResponse>> PostYoutubeVideo(
            [FromQuery] CreateYoutubeVideoRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.CreateVideoAsync(
                request.VideoUrl,
                request.ChannelSuffix,
                cancellationToken);
            if (result.NotFound)
            {
                return NotFound(result.ErrorMessage);
            }

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return CreatedAtAction(
                nameof(GetYTChannel),
                new { id = result.Channel!.Id },
                MapToVideoResponse(result.Video!, result.Channel!));
        }

        private static YoutubeChannelResponse MapToChannelResponse(YTChannel channel)
        {
            return new YoutubeChannelResponse(
                channel.Id,
                channel.Name,
                channel.Description,
                channel.Url.ToString(),
                GetChannelSuffix(channel.Url),
                channel.ActivityLevel,
                channel.LastPublishedAt,
                channel.TitleKeywords,
                channel.CreatedAt,
                channel.UpdatedAt);
        }

        private static YoutubeVideoResponse MapToVideoResponse(YoutubeContent content, YTChannel channel)
        {
            return new YoutubeVideoResponse(
                content.Id,
                content.VideoId,
                content.VideoTitle,
                $"https://www.youtube.com/watch?v={content.VideoId}",
                content.VideoLength,
                content.VideoPublishedAt,
                content.CreatedAt,
                channel.Id,
                channel.Name,
                channel.Url.ToString(),
                GetChannelSuffix(channel.Url),
                channel.ActivityLevel);
        }

        private static string GetChannelSuffix(Uri url)
        {
            return url.IsAbsoluteUri
                ? url.AbsolutePath.Trim('/')
                : url.OriginalString.Trim('/');
        }
    }
}
