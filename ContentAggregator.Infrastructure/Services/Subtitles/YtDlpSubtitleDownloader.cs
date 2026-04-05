using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ContentAggregator.Application.Interfaces;
using ContentAggregator.Application.Models;
using ContentAggregator.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentAggregator.Infrastructure.Services.Subtitles
{
    public sealed class YtDlpSubtitleDownloader : ISubtitleDownloader
    {
        private readonly ILogger<YtDlpSubtitleDownloader> _logger;
        private readonly string _ytDlpExecutable;

        public YtDlpSubtitleDownloader(
            IOptions<YtDlpOptions> options,
            ILogger<YtDlpSubtitleDownloader> logger)
        {
            _logger = logger;
            _ytDlpExecutable = ResolveYtDlpExecutable(options.Value);
        }

        public async Task<DownloadedSubtitle?> DownloadAsync(string videoId, CancellationToken cancellationToken)
        {
            var tempDir = CreateTempDirectory(Path.GetTempPath());
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpExecutable,
                    Arguments = $"--write-sub --write-auto-sub --sub-langs \"ka-orig,ka,en-orig,en,ru-orig,ru\" --sub-format \"srt\" --skip-download \"https://www.youtube.com/watch?v={videoId}\"",
                    WorkingDirectory = tempDir,
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
                catch (Win32Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Unable to start yt-dlp using '{_ytDlpExecutable}'. Ensure the executable exists and is callable.",
                        ex);
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    if (process.ExitCode == 1 && error.Contains("WARNING: ffmpeg not found.", StringComparison.Ordinal))
                    {
                        _logger.LogInformation(
                            "ffmpeg is not needed for subtitle download when no video file is downloaded. VideoId: {VideoId}",
                            videoId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}: {error}");
                    }
                }

                var combinedOutput = $"{output}{Environment.NewLine}{error}";
                if (combinedOutput.Contains("There are no subtitles for the requested languages", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("No subtitles were found for videoId {VideoId}.", videoId);
                    return null;
                }

                var subtitleFile = Directory.GetFiles(tempDir, "*.srt")
                    .OrderBy(GetSubtitlePriority)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(subtitleFile))
                {
                    return null;
                }

                var originalSrt = await File.ReadAllTextAsync(subtitleFile, cancellationToken);
                if (string.IsNullOrWhiteSpace(originalSrt))
                {
                    return null;
                }

                return new DownloadedSubtitle(
                    originalSrt,
                    SrtToText(originalSrt),
                    ResolveSubtitleLanguageFromPath(subtitleFile));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private string ResolveYtDlpExecutable(YtDlpOptions options)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var commandName = isWindows ? "yt-dlp.exe" : "yt-dlp";

            if (!string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                var resolvedConfiguredPath = ResolveConfiguredPath(options.ExecutablePath, commandName);
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

        private static string CreateTempDirectory(string initialPath)
        {
            var tempDir = Path.Combine(initialPath, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static string SrtToText(string srtContent)
        {
            var cleanedLines = new List<string>();
            string? previousLine = null;

            using var reader = new StringReader(srtContent);
            while (reader.ReadLine() is { } line)
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

                if (trimmed == previousLine)
                {
                    continue;
                }

                cleanedLines.Add(trimmed);
                previousLine = trimmed;
            }

            return string.Join(Environment.NewLine, cleanedLines);
        }

        private static bool IsSubtitleMetadata(string line)
        {
            return string.IsNullOrWhiteSpace(line)
                   || int.TryParse(line.Trim(), out _)
                   || line.Contains("-->", StringComparison.Ordinal);
        }

        private static int GetSubtitlePriority(string subtitlePath)
        {
            var languageTag = GetLanguageTag(subtitlePath);

            return languageTag switch
            {
                "ka-orig" => 0,
                "ka" => 1,
                "en-orig" => 2,
                "en" => 3,
                "ru-orig" => 4,
                "ru" => 5,
                _ => 10
            };
        }

        private static SubtitleLanguage ResolveSubtitleLanguageFromPath(string subtitlePath)
        {
            var languageTag = GetLanguageTag(subtitlePath);
            if (languageTag.StartsWith("ka", StringComparison.OrdinalIgnoreCase))
            {
                return SubtitleLanguage.Georgian;
            }

            if (languageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return SubtitleLanguage.English;
            }

            if (languageTag.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            {
                return SubtitleLanguage.Russian;
            }

            return string.IsNullOrWhiteSpace(languageTag)
                ? SubtitleLanguage.Unknown
                : SubtitleLanguage.Other;
        }

        private static string GetLanguageTag(string subtitlePath)
        {
            var withoutExtension = Path.GetFileNameWithoutExtension(subtitlePath);
            var lastDotIndex = withoutExtension.LastIndexOf('.');
            if (lastDotIndex < 0 || lastDotIndex == withoutExtension.Length - 1)
            {
                return string.Empty;
            }

            return withoutExtension[(lastDotIndex + 1)..].ToLowerInvariant();
        }
    }
}
