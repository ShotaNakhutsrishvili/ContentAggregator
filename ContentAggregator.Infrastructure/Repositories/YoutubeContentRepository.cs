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

        public async Task<YoutubeContent?> GetYTContentsByIdAsync(int id)
        {
            return await _context.YoutubeContents.FindAsync(id);
        }

        public async Task<List<YoutubeContent>> GetYTContentsNeedingRefetch()
        {
            return await _context.YoutubeContents.Where(x => x.NeedsRefetch).ToListAsync();
        }

        public async Task<List<YoutubeContent>> GetYTContentsWithoutSubtitles()
        {
            return await _context.YoutubeContents
                .Where(x => string.IsNullOrEmpty(x.SubtitlesOrigSRT) && !x.NeedsRefetch)
                .ToListAsync();
        }

        public async Task<List<YoutubeContent>> GetYTContentsWithoutSummaries()
        {
            return await _context.YoutubeContents
                .Where(x => x.SubtitlesFiltered != null
                            && (string.IsNullOrEmpty(x.VideoSummary)
                                || string.IsNullOrEmpty(x.YoutubeCommentText)))
                .ToListAsync();
        }

        public async Task<List<YoutubeContent>> GetYTContentsForFBPost()
        {
            return await _context.YoutubeContents
                .Where(x => !string.IsNullOrEmpty(x.VideoSummary) && !x.FbPosted)
                .ToListAsync();
        }

        public async Task<List<YoutubeContent>> GetYTContentsForYoutubeCommentPost()
        {
            return await _context.YoutubeContents
                .Where(x => !string.IsNullOrEmpty(x.YoutubeCommentText) && !x.YoutubeCommentPosted)
                .ToListAsync();
        }

        public async Task AddYTContents(List<YoutubeContent> contents)
        {
            await _context.YoutubeContents.AddRangeAsync(contents);
            await _context.SaveChangesAsync();
        }

        public async Task AddYTContentFeature(YoutubeContentFeature contentFeature)
        {
            _context.YoutubeContentFeatures.Add(contentFeature);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateYTContentsAsync(YoutubeContent yTContent)
        {
            _context.YoutubeContents.Update(yTContent);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateYTContentsRangeAsync(List<YoutubeContent> yTContents)
        {
            _context.YoutubeContents.UpdateRange(yTContents);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteYTContentAsync(int id, CancellationToken cancellationToken)
        {
            var yTContent = await _context.YoutubeContents.FindAsync(new object[] { id }, cancellationToken);
            if (yTContent == null)
            {
                return false;
            }

            _context.YoutubeContents.Remove(yTContent);
            await _context.SaveChangesAsync(cancellationToken);
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
    }
}
