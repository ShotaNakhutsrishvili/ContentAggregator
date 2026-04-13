using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Services.Features
{
    public sealed class FeatureService : IFeatureService
    {
        private readonly IFeatureRepository _featureRepository;

        public FeatureService(IFeatureRepository featureRepository)
        {
            _featureRepository = featureRepository;
        }

        public async Task<IReadOnlyList<Feature>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _featureRepository.GetAllFeaturesAsync(cancellationToken);
        }

        public async Task<Feature?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            return await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
        }

        public async Task<Feature?> UpdateAsync(
            int id,
            string firstNameEng,
            string lastNameEng,
            string firstNameGeo,
            string lastNameGeo,
            CancellationToken cancellationToken)
        {
            var existingFeature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
            if (existingFeature == null)
            {
                return null;
            }

            existingFeature.FirstNameEng = firstNameEng;
            existingFeature.LastNameEng = lastNameEng;
            existingFeature.FirstNameGeo = firstNameGeo;
            existingFeature.LastNameGeo = lastNameGeo;
            existingFeature.UpdatedAt = DateTimeOffset.UtcNow;

            await _featureRepository.SaveChangesAsync(cancellationToken);
            return existingFeature;
        }

        public async Task<Feature> CreateAsync(
            string firstNameEng,
            string lastNameEng,
            string firstNameGeo,
            string lastNameGeo,
            CancellationToken cancellationToken)
        {
            var featureEntity = new Feature
            {
                FirstNameEng = firstNameEng,
                LastNameEng = lastNameEng,
                FirstNameGeo = firstNameGeo,
                LastNameGeo = lastNameGeo
            };

            await _featureRepository.AddFeatureAsync(featureEntity, cancellationToken);
            await _featureRepository.SaveChangesAsync(cancellationToken);
            return featureEntity;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            var deleted = await _featureRepository.DeleteFeatureAsync(id, cancellationToken);
            if (!deleted)
            {
                return false;
            }

            await _featureRepository.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
