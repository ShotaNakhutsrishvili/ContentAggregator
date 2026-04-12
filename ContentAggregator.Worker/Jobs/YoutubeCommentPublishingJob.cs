using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.Worker.Jobs
{
    public sealed class YoutubeCommentPublishingJob
    {
        private readonly IYoutubeCommentWorkflow _workflow;

        public YoutubeCommentPublishingJob(IYoutubeCommentWorkflow workflow)
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
