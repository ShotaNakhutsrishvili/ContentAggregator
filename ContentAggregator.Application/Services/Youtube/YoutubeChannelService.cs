using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Application.Models.Youtube;
using ContentAggregator.Core.Entities;

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

        public async Task<IReadOnlyList<YoutubeChannelListItemResponse>> GetAllAsync(CancellationToken cancellationToken)
        {
            var channels = await _channelRepository.GetAllChannelsAsync(cancellationToken);

            return channels
                .Select(MapToListItemResponse)
                .ToList();
        }

        public async Task<YoutubeChannelDetailResponse?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            var channel = await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
            return channel == null ? null : MapToDetailResponse(channel);
        }

        public async Task<YoutubeChannelDetailResponse?> UpdateAsync(
            string id,
            UpdateYoutubeChannelRequest request,
            CancellationToken cancellationToken)
        {
            var existingChannel = await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
            if (existingChannel == null)
            {
                return null;
            }

            existingChannel.Name = string.IsNullOrWhiteSpace(request.ChannelTitle)
                ? existingChannel.Name
                : request.ChannelTitle;
            existingChannel.Url = BuildYoutubeChannelUri(request.ChannelSuffix);
            existingChannel.ActivityLevel = request.ActivityLevel;
            existingChannel.TitleKeywords = request.TitleKeywords;
            existingChannel.UpdatedAt = DateTimeOffset.UtcNow;

            await _channelRepository.SaveChangesAsync(cancellationToken);
            return MapToDetailResponse(existingChannel);
        }

        public async Task<CreateYoutubeChannelResult> CreateAsync(
            CreateYoutubeChannelRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var searchResults = await _youtubeMetadataClient.SearchChannelsAsync(
                    request.ChannelSuffix,
                    cancellationToken);
                if (searchResults.Count == 0)
                {
                    return new CreateYoutubeChannelResult(
                        null,
                        "No YouTube channel was found for the provided channel suffix.");
                }

                YoutubeChannelSearchMatch match;
                if (searchResults.Count > 1)
                {
                    if (string.IsNullOrWhiteSpace(request.ChannelTitle))
                    {
                        return new CreateYoutubeChannelResult(
                            null,
                            "Several channels with provided 'channelSuffix' were found. Please provide 'channelTitle' for clarity.");
                    }

                    match = searchResults.SingleOrDefault(x =>
                               string.Equals(x.Title, request.ChannelTitle, StringComparison.Ordinal))
                           ?? throw new InvalidOperationException(
                               $"No channel matched the provided channelTitle '{request.ChannelTitle}'.");
                }
                else
                {
                    match = searchResults[0];
                }

                if (await _channelRepository.GetChannelByIdAsync(match.ChannelId, cancellationToken) != null)
                {
                    return new CreateYoutubeChannelResult(null, "Channel with the Channel ID already exists.");
                }

                var channel = new YTChannel
                {
                    Name = match.Title,
                    Id = match.ChannelId,
                    Url = BuildYoutubeChannelUri(request.ChannelSuffix),
                    ActivityLevel = request.ActivityLevel,
                    TitleKeywords = request.TitleKeywords
                };

                await _channelRepository.AddChannelAsync(channel, cancellationToken);
                return new CreateYoutubeChannelResult(MapToDetailResponse(channel), null);
            }
            catch (Exception ex)
            {
                return new CreateYoutubeChannelResult(null, ex.Message);
            }
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            return await _channelRepository.DeleteChannelAsync(id, cancellationToken);
        }

        public async Task<CreateYoutubeVideoResult> CreateVideoAsync(
            CreateYoutubeVideoRequest request,
            CancellationToken cancellationToken)
        {
            var videoId = GetVideoId(request.VideoUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return new CreateYoutubeVideoResult(null, false, "Invalid YouTube video URL.");
            }

            YTChannel? channel = null;

            if (!string.IsNullOrWhiteSpace(request.ChannelSuffix))
            {
                var channelUrl = BuildYoutubeChannelUri(request.ChannelSuffix);
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
                    return new CreateYoutubeVideoResult(null, true, "Video not found on YouTube.");
                }

                if (channel == null)
                {
                    var resolvedChannelSuffix = await ResolveChannelSuffixAsync(
                        video.ChannelId,
                        request.ChannelSuffix,
                        cancellationToken);
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

                return new CreateYoutubeVideoResult(
                    MapToVideoResponse(youtubeContent, channel),
                    false,
                    null);
            }
            catch (Exception ex)
            {
                return new CreateYoutubeVideoResult(null, false, ex.Message);
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

        private static YoutubeChannelListItemResponse MapToListItemResponse(YTChannel channel)
        {
            return new YoutubeChannelListItemResponse(
                channel.Id,
                channel.Name,
                channel.Url.ToString(),
                GetChannelSuffix(channel.Url),
                channel.ActivityLevel,
                channel.LastPublishedAt,
                channel.TitleKeywords,
                channel.CreatedAt,
                channel.UpdatedAt);
        }

        private static YoutubeChannelDetailResponse MapToDetailResponse(YTChannel channel)
        {
            return new YoutubeChannelDetailResponse(
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
