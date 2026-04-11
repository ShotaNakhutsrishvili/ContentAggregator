using ContentAggregator.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ContentAggregator.Application.Services.Subtitles
{
    public sealed class SubtitleWorkflow : ISubtitleWorkflow
    {
        private readonly IYoutubeContentRepository _youtubeContentRepository;
        private readonly ISubtitleDownloader _subtitleDownloader;
        private readonly ILogger<SubtitleWorkflow> _logger;

        public SubtitleWorkflow(
            IYoutubeContentRepository youtubeContentRepository,
            ISubtitleDownloader subtitleDownloader,
            ILogger<SubtitleWorkflow> logger)
        {
            _youtubeContentRepository = youtubeContentRepository;
            _subtitleDownloader = subtitleDownloader;
            _logger = logger;
        }

        public async Task ProcessOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                var youtubeContents = await _youtubeContentRepository.GetYTContentsWithoutSubtitles(cancellationToken);

                foreach (var content in youtubeContents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var downloadedSubtitle = await _subtitleDownloader.DownloadAsync(content.VideoId, cancellationToken);
                        if (downloadedSubtitle == null)
                        {
                            continue;
                        }

                        content.SubtitlesOrigSRT = downloadedSubtitle.OriginalSrt;
                        content.SubtitleLanguage = downloadedSubtitle.Language;
                        content.SubtitlesFiltered = downloadedSubtitle.FilteredText;
                        content.LastProcessingError = null;

                        await _youtubeContentRepository.UpdateYTContentsAsync(content, cancellationToken);
                        await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation(
                            "Subtitles downloaded and filtered successfully for content ID {ContentId}.",
                            content.Id);

                        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(12, 20)), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        content.LastProcessingError = ex.Message;

                        try
                        {
                            await _youtubeContentRepository.UpdateYTContentsAsync(content, cancellationToken);
                            await _youtubeContentRepository.SaveChangesAsync(cancellationToken);
                        }
                        catch (Exception updateEx)
                        {
                            _logger.LogError(
                                updateEx,
                                "Failed to persist subtitle processing error for content ID {ContentId}.",
                                content.Id);
                            throw;
                        }

                        _logger.LogWarning(ex, "Subtitle download failed for content ID {ContentId}.", content.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Workflow} was canceled.", nameof(SubtitleWorkflow));
                throw;
            }
        }
    }
}
