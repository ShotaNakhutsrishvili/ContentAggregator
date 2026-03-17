using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    public class YoutubeCommentService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<YoutubeCommentService> _logger;
        private readonly string _youtubeOAuthAccessToken;

        public YoutubeCommentService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<YoutubeCommentService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClient = httpClientFactory.CreateClient(nameof(YoutubeCommentService));
            _logger = logger;
            _youtubeOAuthAccessToken = configuration["YoutubeOAuthAccessToken"] ?? string.Empty;
        }

        [DisableConcurrentExecution(60 * 10)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_youtubeOAuthAccessToken))
            {
                _logger.LogWarning("Skipping YouTube comment job because YoutubeOAuthAccessToken is not configured.");
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var yTRepository = scope.ServiceProvider.GetRequiredService<IYoutubeContentRepository>();
                var youtubeContents = await yTRepository.GetYTContentsForYoutubeCommentPost();

                if (!youtubeContents.Any())
                {
                    return;
                }

                foreach (var content in youtubeContents)
                {
                    var commentText = BuildCommentText(content.VideoSummaryGeo ?? content.VideoSummaryEng ?? string.Empty);
                    var publishResult = await PublishCommentAsync(content.VideoId, commentText, stoppingToken);

                    if (!publishResult.Success)
                    {
                        content.LastProcessingError = publishResult.Message;
                        _logger.LogWarning("YouTube comment failed for content ID {ContentId}. {Message}", content.Id, publishResult.Message);
                        continue;
                    }

                    content.YoutubeCommentPosted = true;
                    content.YoutubeCommentId = publishResult.CommentId;
                    content.YoutubeCommentPostedAt = DateTimeOffset.UtcNow;
                    content.LastProcessingError = null;
                }

                await yTRepository.UpdateYTContentsRangeAsync(youtubeContents);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Service} threw an exception.", nameof(YoutubeCommentService));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        private async Task<CommentPublishResult> PublishCommentAsync(string videoId, string text, CancellationToken cancellationToken)
        {
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

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/youtube/v3/commentThreads?part=snippet");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _youtubeOAuthAccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new CommentPublishResult(
                    false,
                    $"YouTube API error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}",
                    null);
            }

            try
            {
                var deserialized = JsonSerializer.Deserialize<CommentThreadInsertResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (string.IsNullOrWhiteSpace(deserialized?.Id))
                {
                    return new CommentPublishResult(false, "YouTube API did not return comment ID.", null);
                }

                return new CommentPublishResult(true, "YouTube comment posted successfully.", deserialized.Id);
            }
            catch (JsonException)
            {
                return new CommentPublishResult(false, $"Unable to parse YouTube API response. Body: {responseBody}", null);
            }
        }

        private static string BuildCommentText(string summary)
        {
            var result = summary + Environment.NewLine + Environment.NewLine + Constants.AISummaryDisclaimer;
            const int maxLen = 900;
            if (result.Length <= maxLen)
            {
                return result;
            }

            return result[..(maxLen - 3)] + "...";
        }

        private sealed class CommentThreadInsertResponse
        {
            public string? Id { get; set; }
        }

        private readonly record struct CommentPublishResult(bool Success, string Message, string? CommentId);
    }
}
