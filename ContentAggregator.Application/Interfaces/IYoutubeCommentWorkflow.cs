namespace ContentAggregator.Application.Interfaces
{
    public interface IYoutubeCommentWorkflow
    {
        Task ProcessOnceAsync(CancellationToken cancellationToken);
    }
}
