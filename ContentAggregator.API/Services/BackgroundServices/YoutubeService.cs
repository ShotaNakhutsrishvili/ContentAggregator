using Newtonsoft.Json;
using System.Text.RegularExpressions;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models.YTModels;
using Hangfire;
using static ContentAggregator.API.Program;

namespace ContentAggregator.API.Services.BackgroundServices
{
    public class YoutubeService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _apiKey;
        private readonly ILogger<YoutubeService> _logger;
        private readonly TimeSpan _minimumVideoLength = TimeSpan.FromMinutes(30);

        public YoutubeService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider, IConfiguration configuration, ILogger<YoutubeService> logger)
        {
            _httpClient = httpClientFactory.CreateClient(HttpClientNames.Default);
            _serviceProvider = serviceProvider;
            _logger = logger;
            _apiKey = configuration["YoutubeAccessToken"]!;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(2.5), stoppingToken);
            }
        }

        [DisableConcurrentExecution(60 * 60)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                await ProcessChannelsAsync(stoppingToken);
                await ProcessVideosNeedingRefetchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing YouTube channels.");
            }
        }

        private async Task ProcessVideosNeedingRefetchAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var yTContentRepository = scope.ServiceProvider.GetRequiredService<IYoutubeContentRepository>();

            var videosNeedingRefetch = await yTContentRepository.GetYTContentsNeedingRefetch();

            if (!videosNeedingRefetch.Any())
            {
                _logger.LogInformation("No videos needing refetch.");
                return;
            }

            var joinedVideoIds = string.Join(",", videosNeedingRefetch.Select(x => x.VideoId));
            var requestUrl = $"https://youtube.googleapis.com/youtube/v3/videos?id={joinedVideoIds}&key={_apiKey}&part=contentDetails";
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to fetch video details: {response.StatusCode}");
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var videosResponse = JsonConvert.DeserializeObject<YTVideosResponse>(content)!;

            foreach (var video in videosResponse.Items)
            {
                var duration = ParseIso8601Duration(video.ContentDetails!.Duration);
                var ytContent = videosNeedingRefetch.Single(x => x.VideoId == video.Id);

                if (duration > _minimumVideoLength)
                {
                    ytContent.NeedsRefetch = false;
                    ytContent.VideoLength = duration;
                    await yTContentRepository.UpdateYTContentsAsync(ytContent);
                }
                else if (duration != TimeSpan.Zero) // if not zero, but greater than minimum, delete it
                {
                    await yTContentRepository.DeleteYTContentAsync(ytContent.Id, stoppingToken);
                }
            }
        }

        private async Task ProcessChannelsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var channelRepository = scope.ServiceProvider.GetRequiredService<IYTChannelRepository>();

            _logger.LogInformation("Fetching all YouTube channels from the database.");
            var channels = await channelRepository.GetAllChannelsAsync(stoppingToken);

            foreach (var channel in channels)
            {
                try
                {
                    await ProcessChannelAsync(channel, channelRepository, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to process channel {channel.Id}.");
                }
            }
        }

        private async Task ProcessChannelAsync(YTChannel channel, IYTChannelRepository channelRepository, CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Fetching YouTube contents for channel {channel.Id}.");

            var youtubeContents = await FetchYoutubeContentsAsync(channel); // TODO: This might throw exception. Need to encapsulate the background service in try-catch.
            if (youtubeContents.Any())
            {
                _logger.LogInformation($"Fetched {youtubeContents.Count} new videos for channel {channel.Id}.");

                channel.LastPublishedAt = youtubeContents.Max(x => x.VideoPublishedAt);
                foreach (var youtubeContent in youtubeContents)
                {
                    channel.YoutubeContents.Add(youtubeContent);
                }

                await channelRepository.UpdateChannelAsync(channel, stoppingToken);
            }
        }

        private async Task<List<YoutubeContent>> FetchYoutubeContentsAsync(YTChannel channel)
        {
            var requestUrl = GetSearchEndpointRequestUri(channel.Id, channel.LastPublishedAt);
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to fetch YouTube contents: {response.StatusCode} - {errorContent}");

                return new List<YoutubeContent>();
            }
            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonConvert.DeserializeObject<YTSearchResponse>(content);

            var filteredItems = FilterByKeywords(searchResponse!.Items, channel.TitleKeywords);

            return await MapToYoutubeContentsAsync(filteredItems, channel);
        }

        private async Task<List<YoutubeContent>> MapToYoutubeContentsAsync(List<SearchItem> searchItems, YTChannel channel)
        {
            var longerVideos = await FetchLongerVideosAsync(searchItems, _minimumVideoLength);
            if (!longerVideos.Any())
            {
                return new List<YoutubeContent>();
            }

            return longerVideos.Select(video => new YoutubeContent
            {
                VideoId = video.VideoId,
                VideoTitle = video.VideoTitle,
                ChannelId = channel.Id,
                VideoLength = video.VideoLength,
                VideoPublishedAt = video.PublishedAt,
                NeedsRefetch = video.VideoLength == TimeSpan.Zero
            }).ToList();
        }

        private async Task<List<(string VideoId, string VideoTitle, DateTimeOffset PublishedAt, TimeSpan VideoLength)>> FetchLongerVideosAsync(List<SearchItem> searchItems, TimeSpan minLength)
        {
            if (!searchItems.Any())
            {
                return new List<(string, string, DateTimeOffset, TimeSpan)>();
            }

            var videoIds = string.Join(",", searchItems.Select(x => x.Id.VideoId));

            var requestUrl = $"https://youtube.googleapis.com/youtube/v3/videos?id={videoIds}&key={_apiKey}&part=contentDetails";
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to fetch video details: {response.StatusCode}");
                return new List<(string, string, DateTimeOffset, TimeSpan)>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var videosResponse = JsonConvert.DeserializeObject<YTVideosResponse>(content)!;

            return videosResponse.Items
                .Select(video => new
                {
                    VideoId = video.Id,
                    Duration = ParseIso8601Duration(video.ContentDetails!.Duration),
                    SearchItem = searchItems.Single(x => x.Id.VideoId == video.Id)
                })
                .Where(x => x.Duration > minLength || x.Duration == TimeSpan.Zero)
                .Select(x => (
                    x.VideoId,
                    x.SearchItem.Snippet.Title,
                    x.SearchItem.Snippet.PublishedAt,
                    x.Duration
                ))
                .ToList();
        }

        public static TimeSpan ParseIso8601Duration(string duration) // TODO: add unit tests for edge cases.
        {
            // Some YouTube live streams may report "P0D" while in progress.
            if (string.IsNullOrEmpty(duration) || duration == "P0D")
            {
                // Think of possible solutions. 1. message broker.
                // 2. Setting LastPublishedDate to lower than live. Risks duplicating newer videos that were added after live started but before live finished.
                // 2. might need additional DB trips to check whether such videos exist. Otherwise, don't add newer than livestream videos.
                return TimeSpan.Zero;
            }

            if (!duration.StartsWith("PT"))
                throw new FormatException("Invalid duration format. Expected 'PT' prefix.");

            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            string timePart = duration.Substring(2); // Remove "PT"
            var matches = Regex.Matches(timePart, @"(\d+)(H|M|S)");

            foreach (Match match in matches)
            {
                int value = int.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value;

                switch (unit)
                {
                    case "H":
                        hours = value;
                        break;
                    case "M":
                        minutes = value;
                        break;
                    case "S":
                        seconds = value;
                        break;
                }
            }

            return new TimeSpan(hours, minutes, seconds);
        }

        public static List<SearchItem> FilterByKeywords(List<SearchItem> items, string? keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return items;
            }

            var keywordList = keywords.Split(';').Select(k => k.Trim()).ToList();

            return items.Where(x => keywordList.Any(keyword =>
                x.Snippet.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Snippet.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private Uri GetSearchEndpointRequestUri(string channelId, DateTimeOffset? date)
        {
            var baseUrl = $"https://youtube.googleapis.com/youtube/v3/search?key={_apiKey}&channelId={channelId}&part=snippet&order=date";

            if (date.HasValue)
            {
                string formattedDate = date.Value.AddSeconds(1).ToString("yyyy-MM-ddTHH:mm:ssZ"); // Youtube demands RFC 3339 format for this endpoint.
                return new Uri($"{baseUrl}&publishedAfter={formattedDate}");
            }

            return new Uri($"{baseUrl}&maxResults=25");
        }
    }
}