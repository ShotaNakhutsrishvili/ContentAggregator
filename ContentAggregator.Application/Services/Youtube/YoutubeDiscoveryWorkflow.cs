using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using Microsoft.Extensions.Logging;

namespace ContentAggregator.Application.Services.Youtube
{
    public sealed class YoutubeDiscoveryWorkflow : IYoutubeDiscoveryWorkflow
    {
        private readonly IYTChannelRepository _channelRepository;
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly IYoutubeMetadataClient _youtubeMetadataClient;
        private readonly ILogger<YoutubeDiscoveryWorkflow> _logger;

        private readonly TimeSpan _minimumVideoLength = TimeSpan.FromMinutes(30);

        public YoutubeDiscoveryWorkflow(
            IYTChannelRepository channelRepository,
            IYoutubeContentRepository youtubeContentRepository,
            IYoutubeMetadataClient youtubeMetadataClient,
            ILogger<YoutubeDiscoveryWorkflow> logger)
        {
            _channelRepository = channelRepository;
            _youtubeContentRepository = youtubeContentRepository;
            _youtubeMetadataClient = youtubeMetadataClient;
            _logger = logger;
        }

        public async Task ProcessOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcessChannelsAsync(cancellationToken);
                await ProcessVideosNeedingRefetchAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Workflow} was canceled.", nameof(YoutubeDiscoveryWorkflow));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing YouTube channels.");
            }
        }

        private async Task ProcessVideosNeedingRefetchAsync(CancellationToken cancellationToken)
        {
            var videosNeedingRefetch = await _youtubeContentRepository.GetYTContentsNeedingRefetch(cancellationToken);

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
                    cancellationToken);
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
                    await _youtubeContentRepository.UpdateYTContentsAsync(ytContent, cancellationToken);
                    await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
                }
                else if (video.VideoLength != TimeSpan.Zero)
                {
                    var deleted = await _youtubeContentRepository.DeleteYTContentAsync(ytContent.Id, cancellationToken);
                    if (deleted)
                    {
                        await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessChannelsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching all YouTube channels from the database.");
            var channels = await _channelRepository.GetActiveChannelsForDiscoveryAsync(cancellationToken);

            foreach (var channel in channels)
            {
                try
                {
                    await ProcessChannelAsync(channel, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process channel {ChannelId}.", channel.Id);
                }
            }
        }

        private async Task ProcessChannelAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching YouTube contents for channel {ChannelId}.", channel.Id);

            var youtubeContents = await FetchYoutubeContentsAsync(channel, cancellationToken);
            if (!youtubeContents.Any())
            {
                return;
            }

            _logger.LogInformation("Fetched {Count} new videos for channel {ChannelId}.", youtubeContents.Count, channel.Id);

            channel.LastPublishedAt = youtubeContents.Max(x => x.VideoPublishedAt);
            foreach (var youtubeContent in youtubeContents)
            {
                channel.YoutubeContents.Add(youtubeContent);
            }

            await _channelRepository.UpdateChannelAsync(channel, cancellationToken);
            await _channelRepository.SaveChangesAsync(cancellationToken);
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

        private static List<YoutubeChannelVideoSearchMatch> FilterByKeywords(
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
