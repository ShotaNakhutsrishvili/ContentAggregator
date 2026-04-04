using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
using Hangfire;
using static ContentAggregator.API.Program;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Generates language-preserving summaries and timestamped YouTube comment
    /// text from subtitles, then updates participant links and processing state.
    /// </summary>
    public class SummarizerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SummarizerService> _logger;
        private readonly string _lMStudioApiURL;

        public SummarizerService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SummarizerService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClient = httpClientFactory.CreateClient(HttpClientNames.LongTimeout);
            _logger = logger;
            _lMStudioApiURL = (configuration["LMStudioApiURL"] ?? configuration["LMSTUDIO_API_URL"] ?? string.Empty).TrimEnd('/') + "/";
        }

        [DisableConcurrentExecution(60 * 20)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                _logger.LogInformation("{Now}: Starting summarization pass.", DateTimeOffset.UtcNow);

                var yTRepository = scope.ServiceProvider.GetRequiredService<IYoutubeContentRepository>();
                var featureRepository = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();

                var youtubeContents = await yTRepository.GetYTContentsWithoutSummaries();
                var features = await featureRepository.GetAllFeaturesAsync(stoppingToken);

                foreach (var content in youtubeContents)
                {
                    if (!string.IsNullOrWhiteSpace(content.VideoSummary)
                        && !string.IsNullOrWhiteSpace(content.YoutubeCommentText))
                    {
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation(
                            "{Now}: Requesting summary for youtube content ID {ContentId}.",
                            DateTimeOffset.UtcNow,
                            content.Id);

                        var generated = await GenerateSummaryAsync(
                            content.SubtitlesFiltered!,
                            content.SubtitlesOrigSRT,
                            content.SubtitleLanguage);

                        content.VideoSummary = generated.VideoSummary;
                        content.YoutubeCommentText = generated.YoutubeCommentText;
                        content.LastProcessingError = null;

                        ParseParticipants(generated.Participants, content, features);

                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogInformation(
                            "{Now}: Saved summary for youtube content ID {ContentId}.",
                            DateTimeOffset.UtcNow,
                            content.Id);
                    }
                    catch (Exception ex)
                    {
                        content.LastProcessingError = ex.Message;
                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogWarning(ex, "Failed to summarize youtube content ID {ContentId}.", content.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Service} threw an exception.", nameof(SummarizerService));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task<GeneratedSummaryPayload> GenerateSummaryAsync(
            string filteredTranscript,
            string? originalSrt,
            SubtitleLanguage subtitleLanguage)
        {
            if (string.IsNullOrWhiteSpace(_lMStudioApiURL) || _lMStudioApiURL == "/")
            {
                throw new InvalidOperationException("LM Studio API URL is not configured.");
            }

            var userPrompt = BuildUserPrompt(filteredTranscript, originalSrt, subtitleLanguage);
            var request = new
            {
                model = "meta-llama-3.1-8b-instruct",
                messages = new[]
                {
                    new { role = "system", content = SummarizeInstruction },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.6,
                max_tokens = 1200
            };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_lMStudioApiURL}chat/completions", content);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine(responseBody);

            var deserializedResponse = JsonSerializer.Deserialize<CompletionResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Unexpected empty response from LLM.");

            if (deserializedResponse.Choices.Length == 0)
            {
                throw new InvalidOperationException("LLM returned zero choices.");
            }

            var llmContent = deserializedResponse.Choices[0].Message.Content;
            return ParseGeneratedPayload(llmContent);
        }

        private static string BuildUserPrompt(
            string filteredTranscript,
            string? originalSrt,
            SubtitleLanguage subtitleLanguage)
        {
            var languageHint = subtitleLanguage switch
            {
                SubtitleLanguage.Georgian => "Georgian",
                SubtitleLanguage.English => "English",
                SubtitleLanguage.Russian => "Russian",
                SubtitleLanguage.Other => "Unknown specific language (infer from text)",
                _ => "Unknown"
            };

            return $"""
Language hint: {languageHint}

TIMED_SRT:
{originalSrt ?? ""}

FILTERED_TRANSCRIPT:
{filteredTranscript}
""";
        }

        private GeneratedSummaryPayload ParseGeneratedPayload(string llmContent)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var direct = TryGetUsablePayload(llmContent, options);
            if (direct != null)
            {
                return direct;
            }

            var extractedJson = TryExtractJsonObject(llmContent);
            var extracted = extractedJson != null ? TryGetUsablePayload(extractedJson, options) : null;
            if (extracted != null)
            {
                return extracted;
            }

            throw new InvalidOperationException(
                $"LLM response is not parseable or is missing required fields. Raw output: {Truncate(llmContent, 500)}");
        }

        private static GeneratedSummaryPayload? TryDeserializePayload(string json, JsonSerializerOptions options)
        {
            try
            {
                return JsonSerializer.Deserialize<GeneratedSummaryPayload>(json, options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? TryExtractJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            return text[start..(end + 1)];
        }

        private static GeneratedSummaryPayload NormalizePayload(GeneratedSummaryPayload payload)
        {
            payload.Participants = payload.Participants?.Trim() ?? string.Empty;
            payload.VideoSummary = payload.VideoSummary?.Trim() ?? string.Empty;
            payload.YoutubeCommentText = payload.YoutubeCommentText?.Trim() ?? string.Empty;

            return payload;
        }

        private static GeneratedSummaryPayload? TryGetUsablePayload(string json, JsonSerializerOptions options)
        {
            var payload = TryDeserializePayload(json, options);
            if (payload == null)
            {
                return null;
            }

            var normalized = NormalizePayload(payload);
            return !string.IsNullOrWhiteSpace(normalized.VideoSummary)
                   && !string.IsNullOrWhiteSpace(normalized.YoutubeCommentText)
                ? normalized
                : null;
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
            {
                return value;
            }

            return value[..maxLen];
        }

        private void ParseParticipants(string participants, YoutubeContent yTContent, IEnumerable<Feature> features)
        {
            if (string.IsNullOrWhiteSpace(participants))
            {
                return;
            }

            yTContent.AdditionalComments = participants;
            var listOfParticipants = participants.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var participant in listOfParticipants)
            {
                var participantTrimmed = participant.Trim();
                foreach (var feature in features)
                {
                    var isMatch =
                        string.Equals(participantTrimmed, feature.LastNameEng, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(participantTrimmed, feature.LastNameGeo, StringComparison.OrdinalIgnoreCase);

                    if (!isMatch)
                    {
                        continue;
                    }

                    var alreadyLinked = yTContent.YoutubeContentFeatures.Any(x => x.FeatureId == feature.Id);
                    if (alreadyLinked)
                    {
                        continue;
                    }

                    var contentFeature = new YoutubeContentFeature
                    {
                        YoutubeContentId = yTContent.Id,
                        FeatureId = feature.Id
                    };
                    yTContent.YoutubeContentFeatures.Add(contentFeature);
                }
            }
        }

        private sealed class CompletionResponse
        {
            public Choice[] Choices { get; set; } = [];

            public sealed class Choice
            {
                public Message Message { get; set; } = new();
            }

            public sealed class Message
            {
                public string Content { get; set; } = string.Empty;
            }
        }

        private sealed class GeneratedSummaryPayload
        {
            public string Participants { get; set; } = string.Empty;
            public string VideoSummary { get; set; } = string.Empty;
            public string YoutubeCommentText { get; set; } = string.Empty;
        }

        private const string SummarizeInstruction = """
You are given a podcast/interview transcript.
Return ONLY valid JSON with this schema:
{
  "participants": "comma-separated last names only (can be empty string)",
  "videoSummary": "short neutral summary in the same language as transcript",
  "youtubeCommentText": "broad timestamped outline in the same language, format each line like MM:SS - point"
}

Rules:
- Keep output language the same as transcript language.
- Use around 10 broad timestamp lines in youtubeCommentText. If the subjects change a lot, then use more.
- Do not include markdown fences.
""";
    }
}
