namespace ContentAggregator.Application.Interfaces
{
    public interface IFacebookPublishingWorkflow
    {
        Task ProcessOnceAsync(CancellationToken cancellationToken);
    }
}
