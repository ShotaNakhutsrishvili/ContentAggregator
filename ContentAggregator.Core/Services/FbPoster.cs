using System.Text.Json;

namespace ContentAggregator.Core.Services
{
    public class FbPoster
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;

        public FbPoster(string accessToken, HttpClient httpClient)
        {
            _accessToken = accessToken;
            _httpClient = httpClient;
        }

        public async Task<PublishResult> SharePost(string pageId, string? postUrl, string? message = null, CancellationToken cancellationToken = default)
        {
            if (postUrl == null && message == null)
            {
                throw new InvalidOperationException("Either postUrl or message must be provided.");
            }

            var postParameters = new Dictionary<string, string?>
            {
                ["access_token"] = _accessToken
            };

            if (!string.IsNullOrEmpty(postUrl))
            {
                postParameters["link"] = postUrl;
            }

            if (!string.IsNullOrEmpty(message))
            {
                postParameters["message"] = message;
            }

            var response = await _httpClient.PostAsync(
                $"https://graph.facebook.com/v22.0/{pageId}/feed",
                new FormUrlEncodedContent(postParameters!),
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PublishResult(
                    false,
                    $"Facebook API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}",
                    null);
            }

            try
            {
                var payload = JsonSerializer.Deserialize<FacebookPublishResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PublishResult(
                    !string.IsNullOrWhiteSpace(payload?.Id),
                    string.IsNullOrWhiteSpace(payload?.Id)
                        ? "Facebook API returned success without post ID."
                        : $"Post shared successfully with ID: {payload.Id}",
                    payload?.Id);
            }
            catch (JsonException)
            {
                return new PublishResult(false, $"Facebook API response parse error. Body: {responseBody}", null);
            }
        }

        public readonly record struct PublishResult(bool Success, string Message, string? PostId);

        private sealed class FacebookPublishResponse
        {
            public string? Id { get; set; }
        }
    }
}
