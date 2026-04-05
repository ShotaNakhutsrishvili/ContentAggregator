using ContentAggregator.Application.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Hangfire entrypoint for the summarization workflow.
    /// </summary>
    public class SummarizerJob
    {
        private readonly ISummarizationWorkflow _summarizationWorkflow;

        public SummarizerJob(ISummarizationWorkflow summarizationWorkflow)
        {
            _summarizationWorkflow = summarizationWorkflow;
        }

        [DisableConcurrentExecution(60 * 20)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            await _summarizationWorkflow.ProcessPendingAsync(stoppingToken);
        }
    }
}
