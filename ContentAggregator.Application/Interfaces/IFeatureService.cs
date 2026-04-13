using ContentAggregator.Application.Models.Features;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFeatureService
    {
        Task<IReadOnlyList<Feature>> GetAllAsync(CancellationToken cancellationToken);
        Task<Feature?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<Feature?> UpdateAsync(
            int id,
            FeatureWriteModel model,
            CancellationToken cancellationToken);
        Task<Feature> CreateAsync(
            FeatureWriteModel model,
            CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
