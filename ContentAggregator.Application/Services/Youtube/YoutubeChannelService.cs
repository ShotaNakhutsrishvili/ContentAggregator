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

        public async Task<IReadOnlyList<YTChannel>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _channelRepository.GetAllChannelsForAdminAsync(cancellationToken);
        }

        public async Task<YTChannel?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            return await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
        }

        public async Task<YTChannel?> UpdateAsync(
            string id,
            YoutubeChannelWriteModel model,
            CancellationToken cancellationToken)
        {
            var existingChannel = await _channelRepository.GetChannelByIdAsync(id, cancellationToken);
            if (existingChannel == null)
            {
                return null;
            }

            existingChannel.Name = string.IsNullOrWhiteSpace(model.ChannelTitle)
                ? existingChannel.Name
                : model.ChannelTitle;
            existingChannel.Url = BuildYoutubeChannelUri(model.ChannelSuffix);
            existingChannel.ActivityLevel = model.ActivityLevel;
            existingChannel.TitleKeywords = model.TitleKeywords;
            existingChannel.UpdatedAt = DateTimeOffset.UtcNow;

            await _channelRepository.SaveChangesAsync(cancellationToken);
            return existingChannel;
        }

        public async Task<CreateYoutubeChannelResult> CreateAsync(
            YoutubeChannelWriteModel model,
            CancellationToken cancellationToken)
        {
            try
            {
                var searchResults = await _youtubeMetadataClient.SearchChannelsAsync(
                    model.ChannelSuffix,
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
                    if (string.IsNullOrWhiteSpace(model.ChannelTitle))
                    {
                        return new CreateYoutubeChannelResult(
                            null,
                            "Several channels with provided 'channelSuffix' were found. Please provide 'channelTitle' for clarity.");
                    }

                    match = searchResults.SingleOrDefault(x =>
                               string.Equals(x.Title, model.ChannelTitle, StringComparison.Ordinal))
                           ?? throw new InvalidOperationException(
                               $"No channel matched the provided channelTitle '{model.ChannelTitle}'.");
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
                    Url = BuildYoutubeChannelUri(model.ChannelSuffix),
                    ActivityLevel = model.ActivityLevel,
                    TitleKeywords = model.TitleKeywords
                };

                await _channelRepository.AddChannelAsync(channel, cancellationToken);
                await _channelRepository.SaveChangesAsync(cancellationToken);
                return new CreateYoutubeChannelResult(channel, null);
            }
            catch (Exception ex)
            {
                return new CreateYoutubeChannelResult(null, ex.Message);
            }
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            var deleted = await _channelRepository.DeleteChannelAsync(id, cancellationToken);
            if (!deleted)
            {
                return false;
            }

            await _channelRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<CreateYoutubeVideoResult> CreateVideoAsync(
            CreateYoutubeVideoInput input,
            CancellationToken cancellationToken)
        {
            var videoId = GetVideoId(input.VideoUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return new CreateYoutubeVideoResult(null, null, false, "Invalid YouTube video URL.");
            }

            YTChannel? channel = null;

            if (!string.IsNullOrWhiteSpace(input.ChannelSuffix))
            {
                var channelUrl = BuildYoutubeChannelUri(input.ChannelSuffix);
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
                    var resolvedChannelSuffix = await ResolveChannelSuffixAsync(
                        video.ChannelId,
                        input.ChannelSuffix,
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
                    youtubeContent,
                    channel,
                    false,
                    null);
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
