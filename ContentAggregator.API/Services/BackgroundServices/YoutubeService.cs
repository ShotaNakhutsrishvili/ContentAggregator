using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Discovers new videos for configured YouTube channels, applies keyword and
    /// minimum-duration filters, and resolves previously unknown live durations
    /// before persisting channel content.
    /// </summary>
    public class YoutubeService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IYoutubeMetadataClient _youtubeMetadataClient;
        private readonly ILogger<YoutubeService> _logger;

        private readonly TimeSpan _minimumVideoLength = TimeSpan.FromMinutes(30);

        public YoutubeService(
            IServiceProvider serviceProvider,
            IYoutubeMetadataClient youtubeMetadataClient,
            ILogger<YoutubeService> logger)
        {
            _serviceProvider = serviceProvider;
            _youtubeMetadataClient = youtubeMetadataClient;
            _logger = logger;
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

            IReadOnlyList<YoutubeVideoMetadata> videos;
            try
            {
                videos = await _youtubeMetadataClient.GetVideosAsync(
                    videosNeedingRefetch.Select(x => x.VideoId),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch video details for refetch.");
                return;
            }

            foreach (var video in videos)
            {
                var ytContent = videosNeedingRefetch.Single(x => x.VideoId == video.VideoId);

                if (video.VideoLength > _minimumVideoLength)
                {
                    ytContent.NeedsRefetch = false;
                    ytContent.VideoLength = video.VideoLength;
                    await yTContentRepository.UpdateYTContentsAsync(ytContent);
                }
                else if (video.VideoLength != TimeSpan.Zero) // if resolved and shorter than minimum, delete it
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

            var youtubeContents = await FetchYoutubeContentsAsync(channel, stoppingToken);
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

        private async Task<List<YoutubeContent>> FetchYoutubeContentsAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            try
            {
                var searchResults = await _youtubeMetadataClient.SearchChannelVideosAsync(
                    channel.Id,
                    channel.LastPublishedAt,
                    cancellationToken);
                var filteredItems = FilterByKeywords(searchResults, channel.TitleKeywords);
                return await MapToYoutubeContentsAsync(filteredItems, channel, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch YouTube contents for channel {ChannelId}.", channel.Id);
                return new List<YoutubeContent>();
            }
        }

        private async Task<List<YoutubeContent>> MapToYoutubeContentsAsync(
            IReadOnlyList<YoutubeChannelVideoSearchMatch> searchItems,
            YTChannel channel,
            CancellationToken cancellationToken)
        {
            if (!searchItems.Any())
            {
                return new List<YoutubeContent>();
            }

            var videosById = (await _youtubeMetadataClient.GetVideosAsync(
                    searchItems.Select(x => x.VideoId),
                    cancellationToken))
                .ToDictionary(x => x.VideoId, StringComparer.Ordinal);

            if (!videosById.Any())
            {
                return new List<YoutubeContent>();
            }

            return searchItems
                .Where(searchItem =>
                    videosById.TryGetValue(searchItem.VideoId, out var video)
                    && (video.VideoLength > _minimumVideoLength || video.VideoLength == TimeSpan.Zero))
                .Select(searchItem =>
                {
                    var video = videosById[searchItem.VideoId];
                    return new YoutubeContent
                    {
                        VideoId = searchItem.VideoId,
                        VideoTitle = searchItem.Title,
                        ChannelId = channel.Id,
                        VideoLength = video.VideoLength,
                        VideoPublishedAt = searchItem.PublishedAt,
                        NeedsRefetch = video.VideoLength == TimeSpan.Zero
                    };
                })
                .ToList();
        }

        public static List<YoutubeChannelVideoSearchMatch> FilterByKeywords(
            IReadOnlyList<YoutubeChannelVideoSearchMatch> items,
            string? keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return items.ToList();
            }

            var keywordList = keywords.Split(';').Select(k => k.Trim()).ToList();

            return items.Where(x => keywordList.Any(keyword =>
                x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
}
