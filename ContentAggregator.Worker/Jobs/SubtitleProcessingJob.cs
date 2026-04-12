using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.Worker.Jobs
{
    public sealed class SubtitleProcessingJob
    {
        private readonly ISubtitleWorkflow _workflow;

        public SubtitleProcessingJob(ISubtitleWorkflow workflow)
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
