using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models;
using Microsoft.Extensions.Options;

namespace ContentAggregator.API.Services.Summarization
{
    public sealed class LmStudioSummaryGenerator : ISummaryGenerator
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly LmStudioOptions _options;

        public LmStudioSummaryGenerator(HttpClient httpClient, IOptions<LmStudioOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<SummaryGenerationResult> GenerateAsync(
            string filteredTranscript,
            string? originalSrt,
            SubtitleLanguage subtitleLanguage,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                throw new InvalidOperationException("LM Studio API URL is not configured.");
            }

            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException($"LM Studio API URL '{_options.BaseUrl}' is invalid.");
            }

            var request = new CompletionRequest
            {
                Model = _options.Model,
                Messages =
                [
                    new CompletionMessage { Role = "system", Content = SummarizeInstruction },
                    new CompletionMessage
                    {
                        Role = "user",
                        Content = BuildUserPrompt(filteredTranscript, originalSrt, subtitleLanguage)
                    }
                ],
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var deserializedResponse = JsonSerializer.Deserialize<CompletionResponse>(responseBody, SerializerOptions)
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

        private static SummaryGenerationResult ParseGeneratedPayload(string llmContent)
        {
            var direct = TryGetUsablePayload(llmContent);
            if (direct != null)
            {
                return direct;
            }

            var extractedJson = TryExtractJsonObject(llmContent);
            var extracted = extractedJson != null ? TryGetUsablePayload(extractedJson) : null;
            if (extracted != null)
            {
                return extracted;
            }

            throw new InvalidOperationException(
                $"LLM response is not parseable or is missing required fields. Raw output: {Truncate(llmContent, 500)}");
        }

        private static SummaryGenerationResult? TryGetUsablePayload(string json)
        {
            var payload = TryDeserializePayload(json);
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

        private static SummaryGenerationResult? TryDeserializePayload(string json)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<GeneratedSummaryPayload>(json, SerializerOptions);
                return payload == null
                    ? null
                    : new SummaryGenerationResult(
                        payload.Participants ?? string.Empty,
                        payload.VideoSummary ?? string.Empty,
                        payload.YoutubeCommentText ?? string.Empty);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static SummaryGenerationResult NormalizePayload(SummaryGenerationResult payload)
        {
            return payload with
            {
                Participants = payload.Participants?.Trim() ?? string.Empty,
                VideoSummary = payload.VideoSummary?.Trim() ?? string.Empty,
                YoutubeCommentText = payload.YoutubeCommentText?.Trim() ?? string.Empty
            };
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

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
            {
                return value;
            }

            return value[..maxLen];
        }

        private sealed class CompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public CompletionMessage[] Messages { get; set; } = [];

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }
        }

        private sealed class CompletionMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private sealed class CompletionResponse
        {
            [JsonPropertyName("choices")]
            public Choice[] Choices { get; set; } = [];

            public sealed class Choice
            {
                [JsonPropertyName("message")]
                public Message Message { get; set; } = new();
            }

            public sealed class Message
            {
                [JsonPropertyName("content")]
                public string Content { get; set; } = string.Empty;
            }
        }

        private sealed class GeneratedSummaryPayload
        {
            [JsonPropertyName("participants")]
            public string? Participants { get; set; }

            [JsonPropertyName("videoSummary")]
            public string? VideoSummary { get; set; }

            [JsonPropertyName("youtubeCommentText")]
            public string? YoutubeCommentText { get; set; }
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
