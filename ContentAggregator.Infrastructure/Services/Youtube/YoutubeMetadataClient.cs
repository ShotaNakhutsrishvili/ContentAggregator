using System.Text.Json;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Infrastructure.Services.Youtube.Transport;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Infrastructure.Services.Youtube
{
    public sealed class YoutubeMetadataClient : IYoutubeMetadataClient
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly YoutubeApiOptions _options;

        public YoutubeMetadataClient(HttpClient httpClient, IOptions<YoutubeApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<IReadOnlyList<YoutubeChannelSearchMatch>> SearchChannelsAsync(
            string query,
            CancellationToken cancellationToken)
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var searchResponse = await GetRequiredAsync<YoutubeSearchResponse>(
                $"search?part=snippet&q={encodedQuery}&type=channel&key={_options.ApiKey}",
                "Error fetching contents from Youtube",
                cancellationToken);

            return GetRequiredItems(searchResponse.Items, "YouTube channel search")
                .Select(MapChannelSearchMatch)
                .ToList();
        }

        public async Task<IReadOnlyList<YoutubeChannelVideoSearchMatch>> SearchChannelVideosAsync(
            string channelId,
            DateTimeOffset? publishedAfter,
            CancellationToken cancellationToken)
        {
            var publishedAfterQuery = publishedAfter.HasValue
                ? $"&publishedAfter={Uri.EscapeDataString(publishedAfter.Value.AddSeconds(1).ToString("yyyy-MM-ddTHH:mm:ssZ"))}"
                : "&maxResults=25";

            var searchResponse = await GetRequiredAsync<YoutubeSearchResponse>(
                $"search?key={_options.ApiKey}&channelId={Uri.EscapeDataString(channelId)}&part=snippet&order=date{publishedAfterQuery}",
                "Error fetching contents from Youtube",
                cancellationToken);

            return GetRequiredItems(searchResponse.Items, "YouTube channel video search")
                .Select(MapChannelVideoSearchMatch)
                .ToList();
        }

        public async Task<IReadOnlyList<YoutubeVideoMetadata>> GetVideosAsync(
            IEnumerable<string> videoIds,
            CancellationToken cancellationToken)
        {
            var joinedVideoIds = string.Join(",",
                videoIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .Select(Uri.EscapeDataString));

            if (string.IsNullOrWhiteSpace(joinedVideoIds))
            {
                return Array.Empty<YoutubeVideoMetadata>();
            }

            var videosResponse = await GetRequiredAsync<YoutubeVideosResponse>(
                $"videos?part=snippet,contentDetails&id={joinedVideoIds}&key={_options.ApiKey}",
                "Error fetching video details",
                cancellationToken);

            return GetRequiredItems(videosResponse.Items, "YouTube video details")
                .Select(MapVideoMetadata)
                .ToList();
        }

        public async Task<YoutubeVideoMetadata?> GetVideoAsync(string videoId, CancellationToken cancellationToken)
        {
            return (await GetVideosAsync(new[] { videoId }, cancellationToken)).SingleOrDefault();
        }

        public async Task<string?> GetChannelCustomUrlAsync(string channelId, CancellationToken cancellationToken)
        {
            var channelResponse = await GetOptionalAsync<YoutubeChannelsResponse>(
                $"channels?part=snippet&id={Uri.EscapeDataString(channelId)}&key={_options.ApiKey}",
                cancellationToken);
            return channelResponse?.Items?.FirstOrDefault()?.Snippet?.CustomUrl;
        }

        private async Task<T> GetRequiredAsync<T>(
            string requestUri,
            string errorPrefix,
            CancellationToken cancellationToken)
            where T : class
        {
            EnsureConfigured();

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"{errorPrefix}: {response.StatusCode} - {body}");
            }

            return await DeserializeRequiredAsync<T>(response, cancellationToken);
        }

        private async Task<T?> GetOptionalAsync<T>(string requestUri, CancellationToken cancellationToken)
            where T : class
        {
            EnsureConfigured();

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await DeserializeAsync<T>(response, cancellationToken);
        }

        private static YoutubeChannelSearchMatch MapChannelSearchMatch(YoutubeSearchItem item)
        {
            var snippet = item.Snippet
                ?? throw new InvalidOperationException("Channel search response is missing snippet.");

            return new YoutubeChannelSearchMatch(
                RequireText(snippet.ChannelId, "channel search channel ID"),
                RequireText(snippet.Title, "channel search title"));
        }

        private static YoutubeChannelVideoSearchMatch MapChannelVideoSearchMatch(YoutubeSearchItem item)
        {
            var snippet = item.Snippet
                ?? throw new InvalidOperationException("Channel video search response is missing snippet.");
            var itemId = item.Id
                ?? throw new InvalidOperationException("Channel video search response is missing ID.");

            return new YoutubeChannelVideoSearchMatch(
                RequireText(itemId.VideoId, "channel video search video ID"),
                RequireText(snippet.Title, "channel video search title"),
                snippet.Description ?? string.Empty,
                snippet.PublishedAt);
        }

        private static YoutubeVideoMetadata MapVideoMetadata(YoutubeVideoItem item)
        {
            if (item.Snippet == null || item.ContentDetails == null)
            {
                throw new InvalidOperationException("Video response is missing required snippet or content details.");
            }

            return new YoutubeVideoMetadata(
                RequireText(item.Id, "video ID"),
                RequireText(item.Snippet.Title, "video title"),
                RequireText(item.Snippet.ChannelId, "video channel ID"),
                RequireText(item.Snippet.ChannelTitle, "video channel title"),
                item.Snippet.PublishedAt,
                ParseIso8601Duration(RequireText(item.ContentDetails.Duration, "video duration")));
        }

        private static async Task<T> DeserializeRequiredAsync<T>(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
            where T : class
        {
            return await DeserializeAsync<T>(response, cancellationToken)
                ?? throw new InvalidOperationException($"Unable to parse {typeof(T).Name} response from YouTube.");
        }

        private static async Task<T?> DeserializeAsync<T>(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
            where T : class
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(content, SerializerOptions);
        }

        private static IReadOnlyList<T> GetRequiredItems<T>(IReadOnlyList<T>? items, string responseName)
        {
            return items ?? throw new InvalidOperationException($"{responseName} did not include any items.");
        }

        private static string RequireText(string? value, string fieldName)
        {
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"YouTube response is missing required {fieldName}.");
        }

        private static TimeSpan ParseIso8601Duration(string duration)
        {
            if (string.IsNullOrEmpty(duration) || duration == "P0D")
            {
                return TimeSpan.Zero;
            }

            if (!duration.StartsWith("PT", StringComparison.Ordinal))
            {
                throw new FormatException("Invalid duration format. Expected 'PT' prefix.");
            }

            var hours = 0;
            var minutes = 0;
            var seconds = 0;
            var value = 0;

            foreach (var character in duration[2..])
            {
                if (char.IsDigit(character))
                {
                    value = (value * 10) + (character - '0');
                    continue;
                }

                switch (character)
                {
                    case 'H':
                        hours = value;
                        break;
                    case 'M':
                        minutes = value;
                        break;
                    case 'S':
                        seconds = value;
                        break;
                    default:
                        throw new FormatException($"Invalid duration unit '{character}'.");
                }

                value = 0;
            }

            return new TimeSpan(hours, minutes, seconds);
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("YouTube API key is not configured.");
            }
        }
    }
}
