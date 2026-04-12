using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.Worker.Jobs
{
    public sealed class YoutubeDiscoveryJob
    {
        private readonly IYoutubeDiscoveryWorkflow _workflow;

        public YoutubeDiscoveryJob(IYoutubeDiscoveryWorkflow workflow)
        {
            _workflow = workflow;
        }

        [DisableConcurrentExecution(60 * 60)]
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
