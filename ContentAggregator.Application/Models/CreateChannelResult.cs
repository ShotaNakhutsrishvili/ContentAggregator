using ContentAggregator.Core.Entities;

namespace ContentAggregator.Application.Models
{
    public sealed record CreateChannelResult(YTChannel? Channel, string? ErrorMessage)
    {
        public bool Success => Channel != null;
    }
}
