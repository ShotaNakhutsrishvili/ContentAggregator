namespace ContentAggregator.API.Services.Summarization
{
    public sealed class LmStudioOptions
    {
        public const string SectionName = "LmStudio";

        public string BaseUrl { get; set; } = string.Empty;
        public string Model { get; set; } = "meta-llama-3.1-8b-instruct";
        public double Temperature { get; set; } = 0.6;
        public int MaxTokens { get; set; } = 1200;
    }
}
