namespace ContentAggregator.Application.Interfaces
{
    public interface ISubtitleWorkflow
    {
        Task ProcessOnceAsync(CancellationToken cancellationToken);
    }
}
