using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Tasks
{
    /// <summary>
    /// Runs after a library scan and optionally generates subtitles for new media.
    /// </summary>
    public class WhisperPostScanTask : ILibraryPostScanTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WhisperPostScanTask> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWhisperService _whisperService;
        private readonly ISubtitleDetectionService _subtitleDetectionService;

        public WhisperPostScanTask(
            ILibraryManager libraryManager,
            ILogger<WhisperPostScanTask> logger,
            ILoggerFactory loggerFactory)
        {
            _libraryManager  = libraryManager;
            _logger          = logger;
            _loggerFactory   = loggerFactory;

            _whisperService           = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
            _subtitleDetectionService = new SubtitleDetectionService(_loggerFactory.CreateLogger<SubtitleDetectionService>());
        }

        public string Name        => "Whisper Post-Scan Processor";
        public string Key         => "WhisperPostScan";
        public string Description => "Generates subtitles for newly scanned media when enabled in plugin configuration.";

        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

            if (!config.ProcessOnLibraryScan)
            {
                _logger.LogDebug("Post-scan processing disabled in configuration");
                return;
            }

            _logger.LogInformation("Whisper post-scan starting...");

            // Include Video so Home Video libraries are covered
            var items = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[]
                {
                    Jellyfin.Data.Enums.BaseItemKind.Movie,
                    Jellyfin.Data.Enums.BaseItemKind.Episode,
                    Jellyfin.Data.Enums.BaseItemKind.Video
                },
                Recursive = true
            });

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Post-scan task cancelled");
                    break;
                }

                try
                {
                    var path = item.Path;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        continue;

                    if (_subtitleDetectionService.HasSubtitles(
                            path, config.TargetLanguage, config.AIIdentifier) && !config.RegenerateAI)
                    {
                        _logger.LogDebug("Skipping {Path} — subtitle already present", path);
                        continue;
                    }

                    var subtitlePath = _subtitleDetectionService.GetSubtitlePath(
                        path, config.TargetLanguage, config.AIIdentifier, "srt");

                    _logger.LogInformation("Post-scan: generating subtitles for {Path}", path);

                    await _whisperService.GenerateSubtitleAsync(
                        path, subtitlePath,
                        config.WhisperModel.ToString(),
                        config.TargetLanguage,
                        config.TranslateToEnglish,
                        config.WordTimestamps,
                        null,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during post-scan subtitle generation for {Item}", item.Path);
                }
            }

            _logger.LogInformation("Whisper post-scan complete.");
        }
    }
}