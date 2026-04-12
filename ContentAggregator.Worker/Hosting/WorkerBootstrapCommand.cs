namespace ContentAggregator.Worker.Hosting
{
    public sealed class WorkerBootstrapCommand
    {
        private static readonly string[] SupportedSeedFlags =
        [
            "--seed-development-data",
            "--seed"
        ];

        public bool SeedDevelopmentData { get; }

        public string[] HostArguments { get; }

        private WorkerBootstrapCommand(bool seedDevelopmentData, string[] hostArguments)
        {
            SeedDevelopmentData = seedDevelopmentData;
            HostArguments = hostArguments;
        }

        public static bool TryParse(string[] args, out WorkerBootstrapCommand? command)
        {
            command = null;

            if (args.Length == 0 || !string.Equals(args[0], "bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var hostArguments = new List<string>();
            var seedDevelopmentData = false;

            for (var index = 1; index < args.Length; index++)
            {
                var argument = args[index];

                if (SupportedSeedFlags.Contains(argument, StringComparer.OrdinalIgnoreCase))
                {
                    seedDevelopmentData = true;
                    continue;
                }

                hostArguments.Add(argument);
            }

            command = new WorkerBootstrapCommand(seedDevelopmentData, hostArguments.ToArray());
            return true;
        }
    }
}
