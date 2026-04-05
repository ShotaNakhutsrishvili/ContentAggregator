using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    public class YoutubeService
    {
        private readonly IYoutubeDiscoveryWorkflow _workflow;

        public YoutubeService(IYoutubeDiscoveryWorkflow workflow)
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
