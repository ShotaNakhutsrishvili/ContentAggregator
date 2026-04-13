using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFeatureService
    {
        Task<IReadOnlyList<Feature>> GetAllAsync(CancellationToken cancellationToken);
        Task<Feature?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<Feature?> UpdateAsync(
            int id,
            string firstNameEng,
            string lastNameEng,
            string firstNameGeo,
            string lastNameGeo,
            CancellationToken cancellationToken);
        Task<Feature> CreateAsync(
            string firstNameEng,
            string lastNameEng,
            string firstNameGeo,
            string lastNameGeo,
            CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
