using ContentAggregator.Application.Interfaces;
using ContentAggregator.Core.Entities;
using ContentAggregator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Repositories
{
    public class YTChannelRepository : IYTChannelRepository
    {
        private readonly DatabaseContext _context;

        public YTChannelRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<YTChannel?> GetChannelByIdAsync(string id, CancellationToken cancellationToken)
        {
            return await _context.YTChannels.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<YTChannel?> GetChannelByUrlAsync(Uri url, CancellationToken cancellationToken)
        {
            return await _context.YTChannels
             .AsNoTracking()
             .SingleOrDefaultAsync(c => c.Url == url, cancellationToken);
        }

        public async Task<IReadOnlyList<YTChannel>> GetAllChannelsForAdminAsync(CancellationToken cancellationToken)
        {
            return await _context.YTChannels
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<YTChannel>> GetActiveChannelsForDiscoveryAsync(CancellationToken cancellationToken)
        {
            return await _context.YTChannels
                .Where(x => x.ActivityLevel != ChannelActivityLevel.Disabled)
                .ToListAsync(cancellationToken);
        }

        public async Task AddChannelAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            await _context.YTChannels.AddAsync(channel, cancellationToken);
        }

        public async Task<bool> UpdateChannelAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            if (!await ChannelExistsAsync(channel.Id, cancellationToken))
            {
                return false;
            }

            var entry = _context.Entry(channel);
            if (entry.State == EntityState.Detached)
            {
                _context.YTChannels.Attach(channel);
                entry = _context.Entry(channel);
                entry.State = EntityState.Modified;
            }

            return true;
        }

        public async Task<bool> DeleteChannelAsync(string id, CancellationToken cancellationToken)
        {
            var channel = await _context.YTChannels.FindAsync(new object[] { id }, cancellationToken);
            if (channel == null)
            {
                return false;
            }

            _context.YTChannels.Remove(channel);
            return true;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> ChannelExistsAsync(string id, CancellationToken cancellationToken)
        {
            if (_context.YTChannels.Local.Any(x => x.Id == id))
            {
                return true;
            }

            return await _context.YTChannels.AnyAsync(x => x.Id == id, cancellationToken);
        }
    }
}
