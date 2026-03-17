using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
using Hangfire;
using static ContentAggregator.API.Program;

namespace ContentAggregator.API.Services.BackgroundServices
{
    public class SummarizerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SummarizerService> _logger;
        private readonly string _lMStudioApiURL;

        public SummarizerService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SummarizerService> logger)
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

                var youtubeContents = await yTRepository.GetYTContentsWithoutGeoSummaries();
                var features = await featureRepository.GetAllFeaturesAsync(stoppingToken);

                foreach (var content in youtubeContents)
                {
                    if (content.VideoSummaryGeo != null)
                    {
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation("{Now}: Requesting Georgian summary for youtube content ID {ContentId}.", DateTimeOffset.UtcNow, content.Id);
                        var generatedSummaryWithParticipants = await GenerateSummaryAsync(content.SubtitlesFiltered!);

                        var firstLineEndIndex = generatedSummaryWithParticipants.IndexOf('\n');
                        if (firstLineEndIndex <= 0)
                        {
                            content.VideoSummaryGeo = generatedSummaryWithParticipants.Trim();
                        }
                        else
                        {
                            content.VideoSummaryGeo = generatedSummaryWithParticipants.Substring(firstLineEndIndex + 1).Trim();
                            ParseParticipants(generatedSummaryWithParticipants, content, features);
                        }

                        content.LastProcessingError = null;
                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogInformation("{Now}: Saved Georgian summary for youtube content ID {ContentId}.", DateTimeOffset.UtcNow, content.Id);
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

        private async Task<string> GenerateSummaryAsync(string subtitles)
        {
            if (string.IsNullOrWhiteSpace(_lMStudioApiURL) || _lMStudioApiURL == "/")
            {
                throw new InvalidOperationException("LM Studio API URL is not configured.");
            }

            var request = new
            {
                model = "meta-llama-3.1-8b-instruct",
                messages = new[]
                {
                    new { role = "system", content = SummarizeInstruction },
                    new { role = "user", content = subtitles }
                },
                temperature = 0.6,
                max_tokens = 1000
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

            return deserializedResponse.Choices[0].Message.Content;
        }

        private void ParseParticipants(string summary, YoutubeContent yTContent, IEnumerable<Feature> features)
        {
            var firstLineEndIndex = summary.IndexOf('\n');
            if (firstLineEndIndex <= 0)
            {
                return;
            }

            string participants = summary.Substring(0, firstLineEndIndex).Trim();
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

        private const string SummarizeInstruction = """
შენი დავალებაა პოდკასტების, რადიო გადაცემების და ინტერვიუების შეჯამება ქართულად.
პირველ ხაზზე დაწერე მონაწილეების გვარები მძიმით გამოყოფილი და სხვა არაფერი.

შემდეგ აბზაცში დაწერე მოკლე, ზუსტი შეჯამება ქართულად ისე, რომ მონაწილეების სახელები არ გამოიყენო.
გამოიყენე ფრაზები: "მონაწილეები", "საუბარში", "აღინიშნა, რომ...".
მთავარია თემები, იდეები და დასკვნები.
""";
    }
}
