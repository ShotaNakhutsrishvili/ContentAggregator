using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
using ContentAggregator.Core.Models;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Generates language-preserving summaries and timestamped YouTube comment
    /// text from subtitles, then updates participant links and processing state.
    /// </summary>
    public class SummarizerJob
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly ISummaryGenerator _summaryGenerator;
        private readonly ILogger<SummarizerJob> _logger;

        public SummarizerJob(
            IYoutubeContentRepository youtubeContentRepository,
            IFeatureRepository featureRepository,
            ISummaryGenerator summaryGenerator,
            ILogger<SummarizerJob> logger)
        {
            _youtubeContentRepository = youtubeContentRepository;
            _featureRepository = featureRepository;
            _summaryGenerator = summaryGenerator;
            _logger = logger;
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
                _logger.LogInformation("{Now}: Starting summarization pass.", DateTimeOffset.UtcNow);

                var youtubeContents = await _youtubeContentRepository.GetYTContentsWithoutSummaries();
                var features = await _featureRepository.GetAllFeaturesAsync(stoppingToken);

                foreach (var content in youtubeContents)
                {
                    stoppingToken.ThrowIfCancellationRequested();

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

                        var generated = await _summaryGenerator.GenerateAsync(
                            content.SubtitlesFiltered!,
                            content.SubtitlesOrigSRT,
                            content.SubtitleLanguage,
                            stoppingToken);

                        content.VideoSummary = generated.VideoSummary;
                        content.YoutubeCommentText = generated.YoutubeCommentText;
                        content.LastProcessingError = null;

                        ParseParticipants(generated, content, features);

                        await _youtubeContentRepository.UpdateYTContentsAsync(content);
                        _logger.LogInformation(
                            "{Now}: Saved summary for youtube content ID {ContentId}.",
                            DateTimeOffset.UtcNow,
                            content.Id);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        content.LastProcessingError = ex.Message;
                        await _youtubeContentRepository.UpdateYTContentsAsync(content);
                        _logger.LogWarning(ex, "Failed to summarize youtube content ID {ContentId}.", content.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Job} was canceled.", nameof(SummarizerJob));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Job} threw an exception.", nameof(SummarizerJob));
            }
        }

        private static void ParseParticipants(
            SummaryGenerationResult generated,
            YoutubeContent youtubeContent,
            IEnumerable<Feature> features)
        {
            if (string.IsNullOrWhiteSpace(generated.Participants))
            {
                return;
            }

            youtubeContent.AdditionalComments = generated.Participants;
            var listOfParticipants = generated.Participants.Split(
                new[] { ',', ' ' },
                StringSplitOptions.RemoveEmptyEntries);

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

                    var alreadyLinked = youtubeContent.YoutubeContentFeatures.Any(x => x.FeatureId == feature.Id);
                    if (alreadyLinked)
                    {
                        continue;
                    }

                    youtubeContent.YoutubeContentFeatures.Add(new YoutubeContentFeature
                    {
                        YoutubeContentId = youtubeContent.Id,
                        FeatureId = feature.Id
                    });
                }
            }
        }
    }
}
