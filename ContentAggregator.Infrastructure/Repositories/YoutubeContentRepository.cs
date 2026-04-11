using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using ContentAggregator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ContentAggregator.Infrastructure.Repositories
{
    public class YoutubeContentRepository : IYoutubeContentRepository
    {
        private readonly DatabaseContext _context;

        public YoutubeContentRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<YoutubeContent?> GetYTContentsByIdAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<List<YoutubeContent>> GetYTContentsNeedingRefetch(CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents
                .Where(x => x.NeedsRefetch)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<YoutubeContent>> GetYTContentsWithoutSubtitles(CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents
                .Where(x => string.IsNullOrEmpty(x.SubtitlesOrigSRT) && !x.NeedsRefetch)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<YoutubeContent>> GetYTContentsWithoutSummaries(CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents
                .Include(x => x.YoutubeContentFeatures)
                .Where(x => x.SubtitlesFiltered != null
                            && (string.IsNullOrEmpty(x.VideoSummary)
                                || string.IsNullOrEmpty(x.YoutubeCommentText)))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<YoutubeContent>> GetYTContentsForFBPost(CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents
                .Where(x => !string.IsNullOrEmpty(x.VideoSummary) && !x.FbPosted)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<YoutubeContent>> GetYTContentsForYoutubeCommentPost(CancellationToken cancellationToken)
        {
            return await _context.YoutubeContents
                .Where(x => !string.IsNullOrEmpty(x.YoutubeCommentText) && !x.YoutubeCommentPosted)
                .ToListAsync(cancellationToken);
        }

        public async Task AddYTContents(List<YoutubeContent> contents, CancellationToken cancellationToken)
        {
            await _context.YoutubeContents.AddRangeAsync(contents, cancellationToken);
        }

        public Task AddYTContentFeature(YoutubeContentFeature contentFeature, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.YoutubeContentFeatures.Add(contentFeature);
            return Task.CompletedTask;
        }

        public Task UpdateYTContentsAsync(YoutubeContent yTContent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = _context.Entry(yTContent);
            if (entry.State == EntityState.Detached)
            {
                _context.YoutubeContents.Attach(yTContent);
                entry = _context.Entry(yTContent);
                entry.State = EntityState.Modified;
            }

            return Task.CompletedTask;
        }

        public Task UpdateYTContentsRangeAsync(List<YoutubeContent> yTContents, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var content in yTContents)
            {
                var entry = _context.Entry(content);
                if (entry.State == EntityState.Detached)
                {
                    _context.YoutubeContents.Attach(content);
                    entry = _context.Entry(content);
                    entry.State = EntityState.Modified;
                }
            }

            return Task.CompletedTask;
        }

        public async Task<bool> DeleteYTContentAsync(int id, CancellationToken cancellationToken)
        {
            var yTContent = await _context.YoutubeContents.FindAsync(new object[] { id }, cancellationToken);
            if (yTContent == null)
            {
                return false;
            }

            _context.YoutubeContents.Remove(yTContent);
            return true;
        }

        public async Task<PagedYoutubeContentsResult> GetPagedAsync(
            int page,
            int pageSize,
            string? channelId,
            CancellationToken cancellationToken)
        {
            var query = _context.YoutubeContents
                .AsNoTracking()
                .Include(x => x.YTChannel)
                .Where(x => !x.NotRelevant);

            if (!string.IsNullOrWhiteSpace(channelId))
            {
                query = query.Where(x => x.ChannelId == channelId);
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.VideoPublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new YoutubeContentListItem(
                    x.Id,
                    x.VideoId,
                    x.VideoTitle,
                    x.ChannelId,
                    x.YTChannel != null ? x.YTChannel.Name : null,
                    x.VideoPublishedAt,
                    x.VideoLength,
                    x.SubtitleLanguage,
                    x.VideoSummary,
                    x.SubtitlesFiltered != null,
                    x.VideoSummary != null && x.VideoSummary != string.Empty,
                    x.FbPosted,
                    x.YoutubeCommentText != null && x.YoutubeCommentText != string.Empty,
                    x.YoutubeCommentPosted,
                    x.YoutubeCommentId,
                    x.LastProcessingError))
                .ToListAsync(cancellationToken);

            return new PagedYoutubeContentsResult(total, page, pageSize, items);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
