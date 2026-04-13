using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models.Features;
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
            FeatureWriteModel model,
            CancellationToken cancellationToken)
        {
            var existingFeature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
            if (existingFeature == null)
            {
                return null;
            }

            existingFeature.FirstNameEng = model.FirstNameEng;
            existingFeature.LastNameEng = model.LastNameEng;
            existingFeature.FirstNameGeo = model.FirstNameGeo;
            existingFeature.LastNameGeo = model.LastNameGeo;
            existingFeature.UpdatedAt = DateTimeOffset.UtcNow;

            await _featureRepository.SaveChangesAsync(cancellationToken);
            return existingFeature;
        }

        public async Task<Feature> CreateAsync(
            FeatureWriteModel model,
            CancellationToken cancellationToken)
        {
            var featureEntity = new Feature
            {
                FirstNameEng = model.FirstNameEng,
                LastNameEng = model.LastNameEng,
                FirstNameGeo = model.FirstNameGeo,
                LastNameGeo = model.LastNameGeo
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
