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

        public async Task<IReadOnlyList<FeatureListItemResponse>> GetAllAsync(CancellationToken cancellationToken)
        {
            var features = await _featureRepository.GetAllFeaturesAsync(cancellationToken);
            return features
                .Select(MapToListItemResponse)
                .ToList();
        }

        public async Task<FeatureResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var feature = await _featureRepository.GetFeatureByIdAsync(id, cancellationToken);
            return feature == null ? null : MapToResponse(feature);
        }

        public async Task<FeatureResponse?> UpdateAsync(int id, UpdateFeatureRequest feature, CancellationToken cancellationToken)
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
            return MapToResponse(existingFeature);
        }

        public async Task<FeatureResponse> CreateAsync(CreateFeatureRequest feature, CancellationToken cancellationToken)
        {
            var featureEntity = new Feature
            {
                FirstNameEng = feature.FirstNameEng,
                LastNameEng = feature.LastNameEng,
                FirstNameGeo = feature.FirstNameGeo,
                LastNameGeo = feature.LastNameGeo
            };

            await _featureRepository.AddFeatureAsync(featureEntity, cancellationToken);
            await _featureRepository.SaveChangesAsync(cancellationToken);
            return MapToResponse(featureEntity);
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

        private static FeatureListItemResponse MapToListItemResponse(Feature feature)
        {
            return new FeatureListItemResponse(
                feature.Id,
                feature.FirstNameEng,
                feature.LastNameEng,
                feature.FirstNameGeo,
                feature.LastNameGeo,
                feature.CreatedAt,
                feature.UpdatedAt);
        }

        private static FeatureResponse MapToResponse(Feature feature)
        {
            return new FeatureResponse(
                feature.Id,
                feature.FirstNameEng,
                feature.LastNameEng,
                feature.FirstNameGeo,
                feature.LastNameGeo,
                feature.CreatedAt,
                feature.UpdatedAt);
        }
    }
}
