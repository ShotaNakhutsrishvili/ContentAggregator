namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeDiscoveryWorkflow
    {
        Task ProcessOnceAsync(CancellationToken cancellationToken);
    }
}
