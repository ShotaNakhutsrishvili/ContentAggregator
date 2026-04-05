using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using Microsoft.Extensions.Logging;

namespace ContentAggregator.Application.Services.Summarization
{
    public sealed class SummarizationWorkflow : ISummarizationWorkflow
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly ISummaryGenerator _summaryGenerator;
        private readonly ILogger<SummarizationWorkflow> _logger;

        public SummarizationWorkflow(
            IYoutubeContentRepository youtubeContentRepository,
            IFeatureRepository featureRepository,
            ISummaryGenerator summaryGenerator,
            ILogger<SummarizationWorkflow> logger)
        {
            _youtubeContentRepository = youtubeContentRepository;
            _featureRepository = featureRepository;
            _summaryGenerator = summaryGenerator;
            _logger = logger;
        }

        public async Task ProcessPendingAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("{Now}: Starting summarization pass.", DateTimeOffset.UtcNow);

                var youtubeContents = await _youtubeContentRepository.GetYTContentsWithoutSummaries();
                var features = await _featureRepository.GetAllFeaturesAsync(cancellationToken);

                foreach (var content in youtubeContents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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
                            cancellationToken);

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
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Workflow} was canceled.", nameof(SummarizationWorkflow));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Workflow} threw an exception.", nameof(SummarizationWorkflow));
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
