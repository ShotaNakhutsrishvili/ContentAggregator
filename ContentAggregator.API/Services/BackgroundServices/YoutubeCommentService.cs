using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Hangfire entrypoint for YouTube comment publishing.
    /// </summary>
    public class YoutubeCommentService
    {
        private readonly IYoutubeCommentWorkflow _workflow;

        public YoutubeCommentService(IYoutubeCommentWorkflow workflow)
        {
            _workflow = workflow;
        }

        [DisableConcurrentExecution(60 * 10)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            await _workflow.ProcessOnceAsync(stoppingToken);
        }
    }
}
