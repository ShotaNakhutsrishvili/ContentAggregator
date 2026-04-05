using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;
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
        public async Task<ActionResult<IEnumerable<YTChannel>>> GetYTChannels(CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.GetAllAsync(cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<YTChannel>> GetYTChannel(string id, CancellationToken cancellationToken)
        {
            var channel = await _youtubeChannelService.GetByIdAsync(id, cancellationToken);
            if (channel == null)
            {
                return NotFound();
            }

            return Ok(channel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutYTChannel(
            string id,
            [FromHeader(Name = "Prefer")] string? preferHeader,
            [FromBody] YtChannelDto yTChannelDto,
            CancellationToken cancellationToken)
        {
            var existingChannel = await _youtubeChannelService.UpdateAsync(id, yTChannelDto, cancellationToken);
            if (existingChannel == null)
            {
                return NotFound();
            }

            var wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;
            return wantsMinimalResponse ? NoContent() : Ok(existingChannel);
        }

        [HttpPost]
        public async Task<ActionResult<YTChannel>> PostYTChannel(
            [FromBody] YtChannelDto channelDto,
            CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.CreateAsync(channelDto, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return CreatedAtAction(nameof(GetYTChannel), new { id = result.Channel!.Id }, result.Channel);
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
        public async Task<ActionResult<YoutubeContent>> PostYoutubeVideo(
            [FromQuery] Uri videoUrl,
            [FromQuery] string? channelSuffix,
            CancellationToken cancellationToken)
        {
            var result = await _youtubeChannelService.CreateVideoAsync(videoUrl, channelSuffix, cancellationToken);
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
                new { id = result.ChannelId! },
                result.YoutubeContent);
        }
    }
}
