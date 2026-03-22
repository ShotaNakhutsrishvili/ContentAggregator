using Microsoft.AspNetCore.Mvc;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.YTModels;
using Newtonsoft.Json;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models.DTOs;
using static ContentAggregator.API.Program;
using System.Web;
using ContentAggregator.API.Services.BackgroundServices;

namespace ContentAggregator.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YTChannelsController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IYTChannelRepository _yTChannelRepository;
        private readonly string _apiKey;

        public YTChannelsController(IHttpClientFactory httpClientFactory, IYTChannelRepository yTChannelRepository, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient(HttpClientNames.Default);
            _yTChannelRepository = yTChannelRepository;
            _apiKey = configuration["YoutubeAccessToken"]!;
        }

        // GET: api/YTChannels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<YTChannel>>> GetYTChannels(CancellationToken cancellationToken)
        {
            var result = await _yTChannelRepository.GetAllChannelsAsync(cancellationToken);

            return Ok(result);
        }

        // GET: api/YTChannels/5
        [HttpGet("{id}")]
        public async Task<ActionResult<YTChannel>> GetYTChannel(string id, CancellationToken cancellationToken)
        {
            var yTChannel = await _yTChannelRepository.GetChannelByIdAsync(id, cancellationToken);

            if (yTChannel == null)
            {
                return NotFound();
            }

            return Ok(yTChannel);
        }

        // PUT: api/YTChannels/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutYTChannel(string id, [FromHeader(Name = "Prefer")] string? preferHeader, [FromBody] YtChannelDto yTChannelDto, CancellationToken cancellationToken)
        {
            var existingChannel = await _yTChannelRepository.GetChannelByIdAsync(id, cancellationToken);
            if (existingChannel == null)
            {
                return NotFound();
            }

            // TODO: Use automapper instead.
            existingChannel.Name = yTChannelDto.ChannelTitle;
            existingChannel.Url = new Uri($"https://www.youtube.com/{yTChannelDto.ChannelSuffix}");
            existingChannel.ActivityLevel = yTChannelDto.ActivityLevel;
            existingChannel.TitleKeywords = yTChannelDto.TitleKeywords;

            existingChannel.UpdatedAt = DateTimeOffset.UtcNow;

            await _yTChannelRepository.SaveChangesAsync(cancellationToken);

            bool wantsMinimalResponse = preferHeader?.Contains("return=minimal") ?? false;

            return wantsMinimalResponse ? NoContent() : Ok(existingChannel);
        }

        // POST: api/YTChannels
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<YTChannel>> PostYTChannel([FromBody] YtChannelDto channelDTO, CancellationToken cancellationToken)
        {
            try
            {
                var requestUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&q={channelDTO.ChannelSuffix}&type=channel&key={_apiKey}";

                var response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var searchResponse = JsonConvert.DeserializeObject<YTSearchResponse>(content);
                    SearchItem item;
                    if (searchResponse!.Items.Count > 1)
                    {
                        if (channelDTO.ChannelTitle == null)
                        {
                            return BadRequest($"Several channels with provided 'channelSuffix' were found. Please provide 'channelTitle' for clarity.");
                        }
                        item = searchResponse.Items.Single(x => x.Snippet.Title == channelDTO.ChannelTitle);
                    }
                    else
                    {
                        item = searchResponse.Items.Single();
                    }

                    if (await _yTChannelRepository.GetChannelByIdAsync(item.Snippet.ChannelId, cancellationToken) != null)
                    {
                        return BadRequest($"Channel with the Channel ID already exists.");
                    }

                    var yTChannel = new YTChannel
                    {
                        Name = item.Snippet.ChannelTitle,
                        Id = item.Snippet.ChannelId,
                        Url = new Uri($"https://www.youtube.com/{channelDTO.ChannelSuffix}"),
                        ActivityLevel = channelDTO.ActivityLevel,
                        TitleKeywords = channelDTO.TitleKeywords
                    };

                    await _yTChannelRepository.AddChannelAsync(yTChannel, cancellationToken);

                    return CreatedAtAction(nameof(GetYTChannel), new { id = yTChannel.Id }, yTChannel);
                }
                else
                {
                    // log error
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest($"Error fetching contents from Youtube: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                return BadRequest($"Error occured: {ex.Message}");
            }
        }

        // DELETE: api/YTChannels/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteYTChannel(string id, CancellationToken cancellationToken)
        {
            var result = await _yTChannelRepository.DeleteChannelAsync(id, cancellationToken);
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
            // Extract videoId from the video URL
            var videoId = HttpUtility.ParseQueryString(videoUrl.Query).Get("v");
            if (string.IsNullOrEmpty(videoId))
            {
                return BadRequest("Invalid YouTube video URL.");
            }

            YTChannel? channel = null;

            if (!string.IsNullOrEmpty(channelSuffix))
            {
                var channelUrl = new Uri("https://www.youtube.com/" + channelSuffix);
                channel = await _yTChannelRepository.GetChannelByUrlAsync(channelUrl, cancellationToken);
            }

            var videoRequestUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails&id={videoId}&key={_apiKey}";
            var videoHttpResponse = await _httpClient.GetAsync(videoRequestUrl, cancellationToken);

            if (!videoHttpResponse.IsSuccessStatusCode)
            {
                var errorContent = await videoHttpResponse.Content.ReadAsStringAsync();
                return BadRequest($"Error fetching video details: {videoHttpResponse.StatusCode} - {errorContent}");
            }

            var videoContent = await videoHttpResponse.Content.ReadAsStringAsync();
            var videosResponse = JsonConvert.DeserializeObject<YTVideosResponse>(videoContent);

            if (videosResponse?.Items.Count != 1)
            {
                return NotFound("Video not found on YouTube.");
            }

            VideoItem videoItem = videosResponse.Items[0];
            //if (channel != null)
            //{
            //    // Get youtube info
            //    // Save youtube info and return success.
            //}
            if (channel == null)
            {
                var channelId = videoItem.Snippet.ChannelId;
                var channelName = videoItem.Snippet.ChannelTitle;

                // Optionally fetch more channel details using the `channels` endpoint
                var channelRquestUrl = $"https://www.googleapis.com/youtube/v3/channels?part=snippet&id={channelId}&key={_apiKey}";
                var channelHttpResponse = await _httpClient.GetAsync(channelRquestUrl, cancellationToken);

                if (channelHttpResponse.IsSuccessStatusCode)
                {
                    var channelContent = await channelHttpResponse.Content.ReadAsStringAsync();
                    var channelsResponse = JsonConvert.DeserializeObject<YTChannelsResponse>(channelContent);
                    channelSuffix = channelsResponse.Items[0].Snippet.CustomUrl;
                }

                channel = new YTChannel
                {
                    Name = channelName,
                    Id = channelId,
                    Url = new Uri($"https://www.youtube.com/{channelSuffix}"),
                    ActivityLevel = ChannelActivityLevel.Disabled, // Disabled channels won't be used in YoutubeService.
                };

                await _yTChannelRepository.AddChannelAsync(channel, cancellationToken);
            }

            // TODO: Move ParseIso8601Duration outside of YoutubeService.Check if it returns proper length before saving to DB

            var youtubeContent = new YoutubeContent
            {
                VideoId = videoId,
                VideoTitle = videoItem.Snippet.Title,
                ChannelId = videoItem.Snippet.ChannelId,
                VideoLength = YoutubeService.ParseIso8601Duration(videoItem.ContentDetails.Duration),
                VideoPublishedAt = videoItem.Snippet.PublishedAt
            };

            channel.YoutubeContents.Add(youtubeContent);
            await _yTChannelRepository.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetYTChannel), new { id = channel.Id }, youtubeContent);

        }
    }
}
