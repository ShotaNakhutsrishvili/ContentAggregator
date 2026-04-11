using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
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

        public async Task<IReadOnlyList<Feature>> GetAllFeaturesAsync(CancellationToken cancellationToken)
        {
            return await _context.Features
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ParticipantFeatureMatch>> GetParticipantMatchesByLastNamesAsync(
            IReadOnlyCollection<string> participantLastNames,
            CancellationToken cancellationToken)
        {
            var normalizedParticipantLastNames = participantLastNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (normalizedParticipantLastNames.Length == 0)
            {
                return [];
            }

            return await _context.Features
                .AsNoTracking()
                .Where(feature =>
                    normalizedParticipantLastNames.Contains(feature.LastNameEng.ToLower()) ||
                    normalizedParticipantLastNames.Contains(feature.LastNameGeo.ToLower()))
                .Select(feature => new ParticipantFeatureMatch(
                    feature.Id,
                    feature.LastNameEng,
                    feature.LastNameGeo))
                .ToListAsync(cancellationToken);
        }

        public async Task AddFeatureAsync(Feature feature, CancellationToken cancellationToken)
        {
            await _context.Features.AddAsync(feature, cancellationToken);
        }

        public async Task<bool> UpdateFeatureAsync(Feature feature, CancellationToken cancellationToken)
        {
            if (!await FeatureExistsAsync(feature.Id, cancellationToken))
            {
                return false;
            }

            var entry = _context.Entry(feature);
            if (entry.State == EntityState.Detached)
            {
                _context.Features.Attach(feature);
                entry = _context.Entry(feature);
                entry.State = EntityState.Modified;
            }

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
            return true;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> FeatureExistsAsync(int id, CancellationToken cancellationToken)
        {
            if (_context.Features.Local.Any(x => x.Id == id))
            {
                return true;
            }

            return await _context.Features.AnyAsync(x => x.Id == id, cancellationToken);
        }
    }
}
