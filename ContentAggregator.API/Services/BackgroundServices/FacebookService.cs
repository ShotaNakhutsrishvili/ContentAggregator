using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Hangfire entrypoint for Facebook publishing.
    /// </summary>
    public class FacebookService
    {
        private readonly IFacebookPublishingWorkflow _workflow;

        public FacebookService(IFacebookPublishingWorkflow workflow)
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
