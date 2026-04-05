using System.Text.Json;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Infrastructure.Services.Facebook
{
    public sealed class FacebookPublisher : IFacebookPublisher
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly FacebookOptions _options;

        public FacebookPublisher(HttpClient httpClient, IOptions<FacebookOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.AccessToken);

        public string? DefaultPageId => _options.PageId;

        public async Task<FacebookPublishResult> SharePostAsync(
            string pageId,
            string? postUrl,
            string? message,
            CancellationToken cancellationToken = default)
        {
            if (postUrl == null && message == null)
            {
                throw new InvalidOperationException("Either postUrl or message must be provided.");
            }

            if (!IsConfigured)
            {
                return new FacebookPublishResult(false, "Facebook access token is not configured.", null);
            }

            var postParameters = new Dictionary<string, string?>
            {
                ["access_token"] = _options.AccessToken
            };

            if (!string.IsNullOrEmpty(postUrl))
            {
                postParameters["link"] = postUrl;
            }

            if (!string.IsNullOrEmpty(message))
            {
                postParameters["message"] = message;
            }

            using var response = await _httpClient.PostAsync(
                $"v22.0/{pageId}/feed",
                new FormUrlEncodedContent(postParameters!),
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new FacebookPublishResult(
                    false,
                    $"Facebook API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}",
                    null);
            }

            try
            {
                var payload = JsonSerializer.Deserialize<FacebookPublishResponse>(responseBody, SerializerOptions);

                return new FacebookPublishResult(
                    !string.IsNullOrWhiteSpace(payload?.Id),
                    string.IsNullOrWhiteSpace(payload?.Id)
                        ? "Facebook API returned success without post ID."
                        : $"Post shared successfully with ID: {payload.Id}",
                    payload?.Id);
            }
            catch (JsonException)
            {
                return new FacebookPublishResult(false, $"Facebook API response parse error. Body: {responseBody}", null);
            }
        }

        private sealed class FacebookPublishResponse
        {
            public string? Id { get; set; }
        }
    }
}
