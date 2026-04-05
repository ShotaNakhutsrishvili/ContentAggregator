using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Infrastructure.Services.YoutubeComments
{
    public sealed class YoutubeCommentPublisher : IYoutubeCommentPublisher
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly YoutubeCommentOptions _options;

        public YoutubeCommentPublisher(HttpClient httpClient, IOptions<YoutubeCommentOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OAuthAccessToken);

        public async Task<YoutubeCommentPublishResult> PublishAsync(
            string videoId,
            string text,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return new YoutubeCommentPublishResult(false, "YouTube OAuth access token is not configured.", null);
            }

            var payload = new
            {
                snippet = new
                {
                    videoId,
                    topLevelComment = new
                    {
                        snippet = new
                        {
                            textOriginal = text
                        }
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "commentThreads?part=snippet");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OAuthAccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new YoutubeCommentPublishResult(
                    false,
                    $"YouTube API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}",
                    null);
            }

            try
            {
                var deserialized = JsonSerializer.Deserialize<CommentThreadInsertResponse>(responseBody, SerializerOptions);
                if (string.IsNullOrWhiteSpace(deserialized?.Id))
                {
                    return new YoutubeCommentPublishResult(false, "YouTube API did not return comment ID.", null);
                }

                return new YoutubeCommentPublishResult(true, "YouTube comment posted successfully.", deserialized.Id);
            }
            catch (JsonException)
            {
                return new YoutubeCommentPublishResult(
                    false,
                    $"Unable to parse YouTube API response. Body: {responseBody}",
                    null);
            }
        }

        private sealed class CommentThreadInsertResponse
        {
            public string? Id { get; set; }
        }
    }
}
