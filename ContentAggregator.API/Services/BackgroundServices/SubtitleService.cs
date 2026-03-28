using System.ComponentModel;
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
                    stoppingToken.ThrowIfCancellationRequested();

                    try
                    {
                        var subtitleSRTFile = await DownloadSubtitlesAsync(content.VideoId, tempDir, stoppingToken);
                        if (string.IsNullOrWhiteSpace(subtitleSRTFile))
                        {
                            continue;
                        }

                        string subtitleSRTString = await File.ReadAllTextAsync(subtitleSRTFile, stoppingToken);
                        string[] subtitleSRTLines = await File.ReadAllLinesAsync(subtitleSRTFile, stoppingToken);

                        if (string.IsNullOrEmpty(subtitleSRTString))
                        {
                            continue;
                        }

                        content.SubtitlesEngSRT = subtitleSRTString;
                        content.SubtitlesFiltered = SRTToText(subtitleSRTLines);
                        content.LastProcessingError = null;
                        await yTRepository.UpdateYTContentsAsync(content);
                        _logger.LogInformation(
                            "Subtitles downloaded and filtered successfully for content ID {ContentId}.",
                            content.Id);

                        var random = new Random();
                        await Task.Delay(TimeSpan.FromSeconds(random.Next(12, 20)), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        content.LastProcessingError = ex.Message;

                        try
                        {
                            await yTRepository.UpdateYTContentsAsync(content);
                        }
                        catch (Exception updateEx)
                        {
                            _logger.LogError(
                                updateEx,
                                "Failed to persist subtitle processing error for content ID {ContentId}.",
                                content.Id);
                            throw;
                        }

                        _logger.LogWarning(ex, "Subtitle download failed for content ID {ContentId}.", content.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Service} was canceled.", nameof(SubtitleService));
                throw;
            }
            finally
            {
                TryDeleteDirectory(tempDir);
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

        private async Task<string?> DownloadSubtitlesAsync(string videoId, string tempDir, CancellationToken stoppingToken)
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
            try
            {
                process.Start();
            }
            catch (Win32Exception ex) // Win32Exception is cross-platform despite the name
            {
                throw new InvalidOperationException(
                    $"Unable to start yt-dlp using '{_ytDlpExecutable}'. Ensure the executable exists and is callable.",
                    ex);
            }

            await process.WaitForExitAsync(stoppingToken);
            var output = await process.StandardOutput.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                if (process.ExitCode == 1 && error.Contains("WARNING: ffmpeg not found."))
                {
                    _logger.LogInformation($"ffmpeg is not needed for subtitle download when we don't download the video itself. VideoId: {videoId}");
                }
                else
                {
                    throw new Exception($"yt-dlp exited with code {process.ExitCode}: {error}");
                }
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

            var preferredGeorgian = files.FirstOrDefault(x => x.Contains(".ka-orig", StringComparison.OrdinalIgnoreCase));
            return preferredGeorgian ?? files.First();
        }

        private void TryDeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary directory '{Path}'.", path);
            }
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
