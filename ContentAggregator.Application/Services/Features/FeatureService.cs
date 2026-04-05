using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Models.DTOs;

namespace ContentAggregator.Application.Services.Features
{
    public sealed class FeatureService : IFeatureService
    {
        private readonly IFeatureRepository _featureRepository;

        public FeatureService(IFeatureRepository featureRepository)
        {
            _featureRepository = featureRepository;
        }

        public async Task<IEnumerable<Feature>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _featureRepository.GetAllFeaturesAsync(cancellationToken);
        }

        public async Task<Feature?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            return await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
        }

        public async Task<Feature?> UpdateAsync(int id, FeatureDto feature, CancellationToken cancellationToken)
        {
            var existingFeature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
            if (existingFeature == null)
            {
                return null;
            }

            existingFeature.FirstNameEng = feature.FirstNameEng;
            existingFeature.LastNameEng = feature.LastNameEng;
            existingFeature.FirstNameGeo = feature.FirstNameGeo;
            existingFeature.LastNameGeo = feature.LastNameGeo;
            existingFeature.UpdatedAt = DateTimeOffset.UtcNow;

            await _featureRepository.SaveChangesAsync(cancellationToken);
            return existingFeature;
        }

        public async Task<Feature> CreateAsync(FeatureDto feature, CancellationToken cancellationToken)
        {
            var featureEntity = new Feature
            {
                FirstNameEng = feature.FirstNameEng,
                LastNameEng = feature.LastNameEng,
                FirstNameGeo = feature.FirstNameGeo,
                LastNameGeo = feature.LastNameGeo
            };

            await _featureRepository.AddFeatureAsync(featureEntity, cancellationToken);
            return featureEntity;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            return await _featureRepository.DeleteFeatureAsync(id, cancellationToken);
        }
    }
}
