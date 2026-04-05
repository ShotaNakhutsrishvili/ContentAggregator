using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;

namespace ContentAggregator.Application.Services.Youtube
{
    public sealed class YoutubeChannelService : IYoutubeChannelService
    {
        private readonly IYTChannelRepository _channelRepository;
        private readonly IYoutubeMetadataClient _youtubeMetadataClient;

        public YoutubeChannelService(
            IYTChannelRepository channelRepository,
            IYoutubeMetadataClient youtubeMetadataClient)
        {
            _channelRepository = channelRepository;
            _youtubeMetadataClient = youtubeMetadataClient;
        }

        public async Task<IEnumerable<YTChannel>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _channelRepository.GetAllChannelsAsync(cancellationToken);
        }

        public async Task<YTChannel?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            return await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
        }

        public async Task<YTChannel?> UpdateAsync(string id, YtChannelDto yTChannelDto, CancellationToken cancellationToken)
        {
            var existingChannel = await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
            if (existingChannel == null)
            {
                return null;
            }

            existingChannel.Name = yTChannelDto.ChannelTitle ?? existingChannel.Name;
            existingChannel.Url = BuildYoutubeChannelUri(yTChannelDto.ChannelSuffix);
            existingChannel.ActivityLevel = yTChannelDto.ActivityLevel;
            existingChannel.TitleKeywords = yTChannelDto.TitleKeywords;
            existingChannel.UpdatedAt = DateTimeOffset.UtcNow;

            await _channelRepository.SaveChangesAsync(cancellationToken);
            return existingChannel;
        }

        public async Task<CreateChannelResult> CreateAsync(YtChannelDto channelDto, CancellationToken cancellationToken)
        {
            try
            {
                var searchResults = await _youtubeMetadataClient.SearchChannelsAsync(channelDto.ChannelSuffix, cancellationToken);
                if (searchResults.Count == 0)
                {
                    return new CreateChannelResult(null, "No YouTube channel was found for the provided channel suffix.");
                }

                YoutubeChannelSearchMatch match;
                if (searchResults.Count > 1)
                {
                    if (string.IsNullOrWhiteSpace(channelDto.ChannelTitle))
                    {
                        return new CreateChannelResult(
                            null,
                            "Several channels with provided 'channelSuffix' were found. Please provide 'channelTitle' for clarity.");
                    }

                    match = searchResults.SingleOrDefault(x =>
                               string.Equals(x.Title, channelDto.ChannelTitle, StringComparison.Ordinal))
                           ?? throw new InvalidOperationException(
                               $"No channel matched the provided channelTitle '{channelDto.ChannelTitle}'.");
                }
                else
                {
                    match = searchResults[0];
                }

                if (await _channelRepository.GetChannelByIdAsync(match.ChannelId, cancellationToken) != null)
                {
                    return new CreateChannelResult(null, "Channel with the Channel ID already exists.");
                }

                var channel = new YTChannel
                {
                    Name = match.Title,
                    Id = match.ChannelId,
                    Url = BuildYoutubeChannelUri(channelDto.ChannelSuffix),
                    ActivityLevel = channelDto.ActivityLevel,
                    TitleKeywords = channelDto.TitleKeywords
                };

                await _channelRepository.AddChannelAsync(channel, cancellationToken);
                return new CreateChannelResult(channel, null);
            }
            catch (Exception ex)
            {
                return new CreateChannelResult(null, ex.Message);
            }
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            return await _channelRepository.DeleteChannelAsync(id, cancellationToken);
        }

        public async Task<CreateYoutubeVideoResult> CreateVideoAsync(
            Uri videoUrl,
            string? channelSuffix,
            CancellationToken cancellationToken)
        {
            var videoId = GetVideoId(videoUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return new CreateYoutubeVideoResult(null, null, false, "Invalid YouTube video URL.");
            }

            YTChannel? channel = null;

            if (!string.IsNullOrWhiteSpace(channelSuffix))
            {
                var channelUrl = BuildYoutubeChannelUri(channelSuffix);
                var existingChannel = await _channelRepository.GetChannelByUrlAsync(channelUrl, cancellationToken);
                if (existingChannel != null)
                {
                    channel = await _channelRepository.GetChannelByIdAsync(existingChannel.Id, cancellationToken);
                }
            }

            try
            {
                var video = await _youtubeMetadataClient.GetVideoAsync(videoId, cancellationToken);
                if (video == null)
                {
                    return new CreateYoutubeVideoResult(null, null, true, "Video not found on YouTube.");
                }

                if (channel == null)
                {
                    var resolvedChannelSuffix = await ResolveChannelSuffixAsync(video.ChannelId, channelSuffix, cancellationToken);
                    channel = new YTChannel
                    {
                        Name = video.ChannelTitle,
                        Id = video.ChannelId,
                        Url = BuildYoutubeChannelUri(resolvedChannelSuffix),
                        ActivityLevel = ChannelActivityLevel.Disabled
                    };

                    await _channelRepository.AddChannelAsync(channel, cancellationToken);
                }

                var youtubeContent = new YoutubeContent
                {
                    VideoId = video.VideoId,
                    VideoTitle = video.Title,
                    ChannelId = channel.Id,
                    VideoLength = video.VideoLength,
                    VideoPublishedAt = video.PublishedAt
                };

                channel.YoutubeContents.Add(youtubeContent);
                await _channelRepository.SaveChangesAsync(cancellationToken);

                return new CreateYoutubeVideoResult(youtubeContent, channel.Id, false, null);
            }
            catch (Exception ex)
            {
                return new CreateYoutubeVideoResult(null, null, false, ex.Message);
            }
        }

        private async Task<string> ResolveChannelSuffixAsync(
            string channelId,
            string? channelSuffix,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(channelSuffix))
            {
                return channelSuffix;
            }

            var customUrl = await _youtubeMetadataClient.GetChannelCustomUrlAsync(channelId, cancellationToken);
            return !string.IsNullOrWhiteSpace(customUrl)
                ? customUrl
                : $"channel/{channelId}";
        }

        private static Uri BuildYoutubeChannelUri(string? suffix)
        {
            var safeSuffix = string.IsNullOrWhiteSpace(suffix) ? string.Empty : suffix.TrimStart('/');
            return new Uri($"https://www.youtube.com/{safeSuffix}");
        }

        private static string? GetVideoId(Uri videoUrl)
        {
            var query = videoUrl.Query.TrimStart('?');
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (!string.Equals(Uri.UnescapeDataString(parts[0]), "v", StringComparison.Ordinal))
                {
                    continue;
                }

                return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }

            return null;
        }
    }
}
