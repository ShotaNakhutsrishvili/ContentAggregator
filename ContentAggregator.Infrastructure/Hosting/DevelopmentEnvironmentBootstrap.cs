using dotenv.net;

namespace ContentAggregator.Infrastructure.Hosting
{
    public static class DevelopmentEnvironmentBootstrap
    {
        /// <summary>
        /// Loads environment variables from the repository-level .env file for local development hosts.
        /// </summary>
        public static void LoadSecretsForDevelopment(string prefix = "..")
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { $"{prefix}/.env" }));
        }
    }
}
