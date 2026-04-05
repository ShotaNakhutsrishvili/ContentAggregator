using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFeatureService
    {
        Task<IEnumerable<Feature>> GetAllAsync(CancellationToken cancellationToken);
        Task<Feature?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<Feature?> UpdateAsync(int id, FeatureDto feature, CancellationToken cancellationToken);
        Task<Feature> CreateAsync(FeatureDto feature, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
