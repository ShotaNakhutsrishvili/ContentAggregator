namespace ContentAggregator.Worker.Hosting
{
    public sealed class BackgroundJobOptions
    {
        public const string SectionName = "BackgroundJobs";

        public string YoutubeDiscoveryCron { get; set; } = "0 */2 * * *";

        public string SubtitleFetchCron { get; set; } = "*/30 * * * *";

        public string SummaryGenerationCron { get; set; } = "*/15 * * * *";

        public string FacebookPublishCron { get; set; } = "*/5 * * * *";

        public string YoutubeCommentPublishCron { get; set; } = "*/10 * * * *";

        public bool RunStartupPipelineInDevelopment { get; set; } = true;
    }
}
