namespace ContentAggregator.API.Services
{
    public static class HostEnvironmentExtensions
    {
        public static bool IsDevOrQA(this IHostEnvironment environment)
        {
            return environment.IsEnvironment("Development") || environment.IsEnvironment("QA");
        }
    }
}
