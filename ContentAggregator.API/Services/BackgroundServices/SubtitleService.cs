using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Hangfire entrypoint for subtitle processing.
    /// </summary>
    public class SubtitleService
    {
        private readonly ISubtitleWorkflow _workflow;

        public SubtitleService(ISubtitleWorkflow workflow)
        {
            _workflow = workflow;
        }

        [DisableConcurrentExecution(60 * 40)]
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
