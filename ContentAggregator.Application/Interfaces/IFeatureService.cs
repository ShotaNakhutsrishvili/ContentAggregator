using ContentAggregator.Application.Models.Features;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFeatureService
    {
        Task<IReadOnlyList<FeatureListItemResponse>> GetAllAsync(CancellationToken cancellationToken);
        Task<FeatureResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<FeatureResponse?> UpdateAsync(int id, UpdateFeatureRequest feature, CancellationToken cancellationToken);
        Task<FeatureResponse> CreateAsync(CreateFeatureRequest feature, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
