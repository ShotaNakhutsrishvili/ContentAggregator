using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Interfaces
{
    public interface IFeatureRepository
    {
        Task<Feature?> GetFeatureByIdAsync(int id, CancellationToken cancellationToken);
        Task<IReadOnlyList<Feature>> GetAllFeaturesAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<ParticipantFeatureMatch>> GetParticipantMatchesByLastNamesAsync(
            IReadOnlyCollection<string> participantLastNames,
            CancellationToken cancellationToken);
        Task AddFeatureAsync(Feature feature, CancellationToken cancellationToken);
        Task<bool> UpdateFeatureAsync(Feature feature, CancellationToken cancellationToken);
        Task<bool> DeleteFeatureAsync(int id, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
