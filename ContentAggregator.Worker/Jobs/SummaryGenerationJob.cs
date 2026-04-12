using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.Worker.Jobs
{
    public sealed class SummaryGenerationJob
    {
        private readonly ISummarizationWorkflow _workflow;

        public SummaryGenerationJob(ISummarizationWorkflow workflow)
        {
            _workflow = workflow;
        }

        [DisableConcurrentExecution(60 * 20)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            await _workflow.ProcessPendingAsync(stoppingToken);
        }
    }
}
