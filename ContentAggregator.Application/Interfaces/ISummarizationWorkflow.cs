namespace ContentAggregator.Application.Interfaces
{
    public interface ISummarizationWorkflow
    {
        Task ProcessPendingAsync(CancellationToken cancellationToken);
    }
}
