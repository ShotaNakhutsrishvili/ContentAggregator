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

                var youtubeContents = await _youtubeContentRepository.GetYTContentsWithoutSummaries(cancellationToken);

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

                        await AddParticipantLinksAsync(generated, content, cancellationToken);

                        await _youtubeContentRepository.UpdateYTContentsAsync(content, cancellationToken);
                        await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
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
                        await _youtubeContentRepository.UpdateYTContentsAsync(content, cancellationToken);
                        await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
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

        private async Task AddParticipantLinksAsync(
            SummaryGenerationResult generated,
            YoutubeContent youtubeContent,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(generated.Participants))
            {
                return;
            }

            youtubeContent.AdditionalComments = generated.Participants;
            var participantLastNames = ParseParticipantLastNames(generated.Participants);
            if (participantLastNames.Count == 0)
            {
                return;
            }

            var participantMatches = await _featureRepository.GetParticipantMatchesByLastNamesAsync(
                participantLastNames,
                cancellationToken);
            var featureIdsByLastName = BuildFeatureIdsByLastName(participantMatches);
            var linkedFeatureIds = youtubeContent.YoutubeContentFeatures
                .Select(x => x.FeatureId)
                .ToHashSet();

            foreach (var participantLastName in participantLastNames)
            {
                if (!featureIdsByLastName.TryGetValue(participantLastName, out var featureIds))
                {
                    continue;
                }

                foreach (var featureId in featureIds)
                {
                    if (!linkedFeatureIds.Add(featureId))
                    {
                        continue;
                    }

                    youtubeContent.YoutubeContentFeatures.Add(new YoutubeContentFeature
                    {
                        YoutubeContentId = youtubeContent.Id,
                        FeatureId = featureId
                    });
                }
            }
        }

        private static IReadOnlyList<string> ParseParticipantLastNames(string participants)
        {
            return participants
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static Dictionary<string, List<int>> BuildFeatureIdsByLastName(
            IReadOnlyList<ParticipantFeatureMatch> participantMatches)
        {
            var featureIdsByLastName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var participantMatch in participantMatches)
            {
                AddFeatureId(featureIdsByLastName, participantMatch.LastNameEng, participantMatch.FeatureId);
                AddFeatureId(featureIdsByLastName, participantMatch.LastNameGeo, participantMatch.FeatureId);
            }

            return featureIdsByLastName;
        }

        private static void AddFeatureId(
            Dictionary<string, List<int>> featureIdsByLastName,
            string lastName,
            int featureId)
        {
            if (!featureIdsByLastName.TryGetValue(lastName, out var featureIds))
            {
                featureIds = [];
                featureIdsByLastName[lastName] = featureIds;
            }

            if (!featureIds.Contains(featureId))
            {
                featureIds.Add(featureId);
            }
        }
    }
}
