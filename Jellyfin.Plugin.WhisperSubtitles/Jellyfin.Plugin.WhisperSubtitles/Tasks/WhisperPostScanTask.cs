using System;
using System.IO;
using System.Linq;
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
    /// Library post-scan task. This runs after a library scan completes and can optionally
    /// generate subtitles for newly added media.
    /// </summary>
    public class WhisperPostScanTask : ILibraryPostScanTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WhisperPostScanTask> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWhisperService _whisperService;
        private readonly ISubtitleDetectionService _subtitleDetectionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperPostScanTask"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="loggerFactory">Logger factory for creating service loggers.</param>
        public WhisperPostScanTask(
            ILibraryManager libraryManager,
            ILogger<WhisperPostScanTask> logger,
            ILoggerFactory loggerFactory)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _loggerFactory = loggerFactory;
            
            // Create service instances directly since Jellyfin 10.11 doesn't support plugin DI registration
            _whisperService = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
            _subtitleDetectionService = new SubtitleDetectionService(_loggerFactory.CreateLogger<SubtitleDetectionService>());
        }

        /// <inheritdoc />
        public string Name => "Whisper Post-Scan Processor";

        /// <inheritdoc />
        public string Key => "WhisperPostScan";

        /// <inheritdoc />
        public string Description => "Generates subtitles for newly scanned media when enabled in plugin configuration.";

        /// <inheritdoc />
        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

            if (!config.ProcessOnLibraryScan)
            {
                _logger.LogDebug("Post-scan processing disabled in configuration");
                return;
            }

            _logger.LogInformation("Whisper post-scan starting. Scanning for new media...");

            // Find recently added items (use library manager's LastScan property)
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Episode },
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
                    {
                        continue;
                    }

                    if (_subtitleDetectionService.HasSubtitles(path, config.TargetLanguage, config.AIIdentifier) && !config.RegenerateAI)
                    {
                        _logger.LogDebug("Skipping {Path}, subtitles already present", path);
                        continue;
                    }

                    var subtitlePath = _subtitleDetectionService.GetSubtitlePath(path, config.TargetLanguage, config.AIIdentifier, "srt");

                    _logger.LogInformation("Generating subtitles for new item: {Path}", path);

                    await _whisperService.GenerateSubtitleAsync(
                        path,
                        subtitlePath,
                        config.WhisperModel,
                        config.TargetLanguage,
                        config.TranslateToEnglish,
                        config.WordTimestamps,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating subtitles during post-scan for {Item}", item.Path);
                }
            }

            _logger.LogInformation("Whisper post-scan processing complete.");
        }
    }
}
