using System.Diagnostics;
using System.Runtime.InteropServices;
using ContentAggregator.Core.Interfaces;
using Hangfire;

namespace ContentAggregator.API.Services.BackgroundServices
{
    /// <summary>
    /// Downloads auto-subtitles (preferring Georgian, then English), normalizes
    /// SRT into plain transcript text, and stores subtitle data for videos
    /// awaiting summarization.
    /// </summary>
    public class SubtitleService : BackgroundService
    {
        private const string YtDlpConfigPathKey = "YtDlp:ExecutablePath";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SubtitleService> _logger;
        private readonly string _ytDlpExecutable;

        public SubtitleService(
            IServiceProvider serviceProvider,
            ILogger<SubtitleService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _ytDlpExecutable = ResolveYtDlpExecutable(configuration);
        }

        [DisableConcurrentExecution(60 * 40)]
        public async Task ProcessOnceAsync()
        {
            await ProcessOnceAsync(CancellationToken.None);
        }

        public async Task ProcessOnceAsync(CancellationToken stoppingToken)
        {
            string tempDir = CreateTempDirectory(Path.GetTempPath());
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var yTRepository = scope.ServiceProvider.GetRequiredService<IYoutubeContentRepository>();
                var youtubeContents = await yTRepository.GetYTContentsWithoutEngSRT();

                foreach (var content in youtubeContents)
                {
                    try
                    {
                        var subtitleSRTFile = await DownloadSubtitlesAsync(content.VideoId, tempDir);
                        if (string.IsNullOrWhiteSpace(subtitleSRTFile))
                        {
                            continue;
                        }

                        string subtitleSRTString = await File.ReadAllTextAsync(subtitleSRTFile!, stoppingToken);
                        string[] subtitleSRTLines = await File.ReadAllLinesAsync(subtitleSRTFile!, stoppingToken);

                        if (string.IsNullOrEmpty(subtitleSRTString))
                        {
                            continue;
                        }

                        content.SubtitlesEngSRT = subtitleSRTString;
                        content.SubtitlesFiltered = SRTToText(subtitleSRTLines);
                        content.LastProcessingError = null;
                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogInformation("Subtitles downloaded and filtered successfully for content ID {ContentId}.", content.Id);

                        var random = new Random();
                        await Task.Delay(TimeSpan.FromSeconds(random.Next(12, 20)), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        content.LastProcessingError = ex.Message;
                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogWarning(ex, "Subtitle download failed for content ID {ContentId}.", content.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Service} threw an exception.", nameof(SubtitleService));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task<string?> DownloadSubtitlesAsync(string videoId, string tempDir)
        {
            string tempDirForSingleSub = CreateTempDirectory(tempDir);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _ytDlpExecutable,
                Arguments = $"--write-sub --write-auto-sub --sub-langs \"en-orig,ka-orig\" --sub-format \"srt\" --skip-download \"https://www.youtube.com/watch?v={videoId}\"",
                WorkingDirectory = tempDirForSingleSub,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"yt-dlp error: {error}");
            }

            if (output.Contains("There are no subtitles for the requested languages", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("No subtitles were found for videoId {VideoId}.", videoId);
                return null;
            }

            var files = Directory.GetFiles(tempDirForSingleSub, "*.srt");
            if (files.Length == 0)
            {
                return null;
            }

            var preferredGeorgian = files.FirstOrDefault(x => x.Contains(".ka.", StringComparison.OrdinalIgnoreCase));
            return preferredGeorgian ?? files.First();
        }

        private string ResolveYtDlpExecutable(IConfiguration configuration)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var commandName = isWindows ? "yt-dlp.exe" : "yt-dlp";
            var configuredPath = configuration[YtDlpConfigPathKey];

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var resolvedConfiguredPath = ResolveConfiguredPath(configuredPath, commandName);
                if (File.Exists(resolvedConfiguredPath))
                {
                    _logger.LogInformation("Using yt-dlp from configured path '{Path}'.", resolvedConfiguredPath);
                    return resolvedConfiguredPath;
                }

                _logger.LogWarning(
                    "Configured yt-dlp path '{Path}' was not found. Falling back to system PATH.",
                    resolvedConfiguredPath);
            }

            _logger.LogInformation("Using yt-dlp from system PATH with command '{Command}'.", commandName);
            return commandName;
        }

        private static string ResolveConfiguredPath(string configuredPath, string commandName)
        {
            var normalizedPath = configuredPath.Trim();
            var resolvedPath = Path.IsPathRooted(normalizedPath)
                ? normalizedPath
                : Path.GetFullPath(normalizedPath);

            if (Directory.Exists(resolvedPath))
            {
                return Path.Combine(resolvedPath, commandName);
            }

            return resolvedPath;
        }

        private static string CreateTempDirectory(string initialPath)
        {
            string tempDir = Path.Combine(initialPath, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static string SRTToText(string[] lines)
        {
            var cleanedLines = new List<string>();
            string? previousLine = null;

            foreach (var line in lines)
            {
                if (IsSubtitleMetadata(line))
                {
                    continue;
                }

                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                if (trimmed != previousLine)
                {
                    cleanedLines.Add(trimmed);
                    previousLine = trimmed;
                }
            }

            return string.Join(Environment.NewLine, cleanedLines);
        }

        private static bool IsSubtitleMetadata(string line)
        {
            return string.IsNullOrWhiteSpace(line)
                   || int.TryParse(line.Trim(), out _)
                   || line.Contains("-->");
        }
    }
}
