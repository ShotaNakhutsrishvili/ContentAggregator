using ContentAggregator.Core.Entities;
using ContentAggregator.Core.Interfaces;
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

        public async Task<IEnumerable<YTChannel>> GetAllChannelsAsync(CancellationToken cancellationToken)
        {
            return await _context.YTChannels
                .Where(x => x.ActivityLevel != ChannelActivityLevel.Disabled)
                .ToListAsync(cancellationToken);
        }

        public async Task AddChannelAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            _context.YTChannels.Add(channel);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> UpdateChannelAsync(YTChannel channel, CancellationToken cancellationToken)
        {
            var existingChannel = await _context.YTChannels.FindAsync(new object[] { channel.Id }, cancellationToken);
            if (existingChannel == null)
            {
                return false;
            }

            _context.YTChannels.Update(channel);
            await _context.SaveChangesAsync(cancellationToken);
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
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
