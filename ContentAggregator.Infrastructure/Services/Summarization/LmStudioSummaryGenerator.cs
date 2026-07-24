using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentAggregator.Infrastructure.Services.Summarization
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

        public string GeneratorModel => _options.Model;

        public string PromptVersion => SummaryPromptVersion;

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

            if (string.IsNullOrWhiteSpace(originalSrt))
            {
                throw new InvalidOperationException("A timed SRT transcript is required to generate content sections.");
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
            return ParseGeneratedPayload(llmContent) with
            {
                GeneratorModel = GeneratorModel,
                PromptVersion = PromptVersion
            };
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
                   && normalized.Sections.Count > 0
                   && normalized.Sections.All(section => !string.IsNullOrWhiteSpace(section.Heading))
                ? normalized
                : null;
        }

        private static SummaryGenerationResult? TryDeserializePayload(string json)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<GeneratedSummaryPayload>(json, SerializerOptions);
                if (payload?.Sections == null
                    || payload.Sections.Any(section =>
                        section?.StartSeconds is null
                        || string.IsNullOrWhiteSpace(section.Heading)
                        || section.Heading.Length > 300))
                {
                    return null;
                }

                var sections = payload.Sections
                    .Select(section => new GeneratedContentSection(
                        section!.StartSeconds!.Value,
                        section.EndSeconds,
                        section.Heading!,
                        section.Summary))
                    .ToArray();

                return new SummaryGenerationResult(
                    payload.Participants ?? string.Empty,
                    payload.VideoSummary ?? string.Empty,
                    sections,
                    string.Empty,
                    string.Empty);
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
                Sections = payload.Sections
                    .Select(section => section with
                    {
                        Heading = section.Heading?.Trim() ?? string.Empty,
                        Summary = section.Summary?.Trim()
                    })
                    .ToArray()
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

            [JsonPropertyName("sections")]
            public GeneratedSectionPayload?[]? Sections { get; set; }
        }

        private sealed class GeneratedSectionPayload
        {
            [JsonPropertyName("startSeconds")]
            public int? StartSeconds { get; set; }

            [JsonPropertyName("endSeconds")]
            public int? EndSeconds { get; set; }

            [JsonPropertyName("heading")]
            public string? Heading { get; set; }

            [JsonPropertyName("summary")]
            public string? Summary { get; set; }
        }

        private const string SummaryPromptVersion = "sections-v1";

        private const string SummarizeInstruction = """
You are given a podcast/interview transcript.
Return ONLY valid JSON with this schema:
{
  "participants": "comma-separated last names only (can be empty string)",
  "videoSummary": "short neutral summary in the same language as transcript",
  "sections": [
    {
      "startSeconds": 0,
      "endSeconds": 120,
      "heading": "concise section heading in the same language as the transcript",
      "summary": "optional one-sentence section summary"
    }
  ]
}

Rules:
- Keep output language the same as transcript language.
- Use TIMED_SRT as the source of section timestamps.
- Use around 10 broad subject-based sections. Use more only when the subject changes materially.
- Return integer seconds for timestamps.
- Sections must be ordered, non-overlapping, and within the video timeline.
- The first section should normally start at 0.
- Do not include markdown fences.
""";
    }
}
