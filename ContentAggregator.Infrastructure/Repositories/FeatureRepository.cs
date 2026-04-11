using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using ContentAggregator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Repositories
{
    public class FeatureRepository : IFeatureRepository
    {
        private readonly DatabaseContext _context;

        public FeatureRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Feature?> GetFeatureByIdAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.Features.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<Feature>> GetAllFeaturesAsync(CancellationToken cancellationToken)
        {
            return await _context.Features
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task AddFeatureAsync(Feature feature, CancellationToken cancellationToken)
        {
            _context.Features.Add(feature);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> UpdateFeatureAsync(Feature feature, CancellationToken cancellationToken)
        {
            var existingFeature = await _context.Features.FindAsync(new object[] { feature.Id }, cancellationToken);
            if (existingFeature == null)
            {
                return false;
            }

            _context.Features.Update(feature);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteFeatureAsync(int id, CancellationToken cancellationToken)
        {
            var feature = await _context.Features.FindAsync(new object[] { id }, cancellationToken);
            if (feature == null)
            {
                return false;
            }

            _context.Features.Remove(feature);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
