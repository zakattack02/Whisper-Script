using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Tasks
{
    /// <summary>
    /// Scheduled task to generate subtitles for videos in the library.
    /// </summary>
    public class WhisperSubtitleTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WhisperSubtitleTask> _logger;
        private readonly ILocalizationManager _localization;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWhisperService _whisperService;
        private readonly ISubtitleDetectionService _subtitleDetectionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperSubtitleTask"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="localization">Localization manager instance.</param>
        /// <param name="loggerFactory">Logger factory for creating service loggers.</param>
        public WhisperSubtitleTask(
            ILibraryManager libraryManager,
            ILogger<WhisperSubtitleTask> logger,
            ILocalizationManager localization,
            ILoggerFactory loggerFactory)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _localization = localization;
            _loggerFactory = loggerFactory;
            
            // Create service instances directly since Jellyfin 10.11 doesn't support plugin DI registration
            _whisperService = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
            _subtitleDetectionService = new SubtitleDetectionService(_loggerFactory.CreateLogger<SubtitleDetectionService>());
        }

        /// <inheritdoc />
        public string Name => "Generate Whisper Subtitles";

        /// <inheritdoc />
        public string Key => "WhisperSubtitleGeneration";

        /// <inheritdoc />
        public string Description => "Generates AI-powered subtitles for videos using OpenAI Whisper";

        /// <inheritdoc />
        public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

            _logger.LogInformation("Starting Whisper subtitle generation task");
            _logger.LogInformation(
                "Configuration: Model={Model}, Language={Language}, Translate={Translate}, AIIdentifier={AIIdentifier}",
                config.WhisperModel, config.TargetLanguage, config.TranslateToEnglish, config.AIIdentifier);

            // Get all video items from the library
            var videoItems = GetVideoItems(config);
            var totalVideos = videoItems.Count;

            if (totalVideos == 0)
            {
                _logger.LogInformation("No videos found to process");
                progress?.Report(100);
                return;
            }

            _logger.LogInformation("Found {Count} videos to process", totalVideos);

            var processedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            foreach (var video in videoItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Task cancelled by user");
                    break;
                }

                try
                {
                    var videoPath = video.Path;
                    _logger.LogInformation("Processing: {VideoPath}", videoPath);

                    // Check if we should skip this video
                    if (ShouldSkipVideo(videoPath, config))
                    {
                        _logger.LogInformation("Skipping: {VideoPath} (already has subtitles)", videoPath);
                        skippedCount++;
                        processedCount++;
                        progress?.Report((double)processedCount / totalVideos * 100);
                        continue;
                    }

                    // Generate subtitle path
                    var subtitlePath = _subtitleDetectionService.GetSubtitlePath(
                        videoPath,
                        config.TargetLanguage,
                        config.AIIdentifier,
                        "srt");

                    // Generate subtitles
                    _logger.LogInformation("Generating subtitles: {SubtitlePath}", subtitlePath);
                    
                    var success = await _whisperService.GenerateSubtitleAsync(
                        videoPath,
                        subtitlePath,
                        config.WhisperModel.ToString(),
                        config.TargetLanguage,
                        config.TranslateToEnglish,
                        config.WordTimestamps,
                        cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation("Successfully generated subtitles for: {VideoPath}", videoPath);
                    }
                    else
                    {
                        _logger.LogError("Failed to generate subtitles for: {VideoPath}", videoPath);
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing video: {VideoPath}", video.Path);
                    errorCount++;
                }

                processedCount++;
                progress?.Report((double)processedCount / totalVideos * 100);
            }

            _logger.LogInformation(
                "Subtitle generation task completed. Processed: {Processed}, Skipped: {Skipped}, Errors: {Errors}",
                processedCount - skippedCount - errorCount, skippedCount, errorCount);
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Don't run automatically - user must trigger manually or schedule it
            return Array.Empty<TaskTriggerInfo>();
        }

        /// <summary>
        /// Get all video items from the library that should be processed.
        /// </summary>
        /// <param name="config">Plugin configuration.</param>
        /// <returns>List of video items.</returns>
        private List<BaseItem> GetVideoItems(PluginConfiguration config)
        {
            // Get enabled libraries from configuration
            var enabledLibraryIds = config.LibrariesToProcess ?? new List<string>();
            
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Video },
                IsVirtualItem = false,
                Recursive = true
            };

            var allItems = _libraryManager.GetItemList(query);
            
            // Filter by enabled libraries and valid paths
            var filteredItems = allItems.Where(item => 
            {
                // Skip items without valid paths
                if (string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path))
                {
                    return false;
                }

                // If no libraries are configured, process all items
                if (!enabledLibraryIds.Any())
                {
                    return true;
                }

                // Find which library folder this item belongs to
                var rootFolder = _libraryManager.GetCollectionFolders(item).FirstOrDefault();
                if (rootFolder == null)
                {
                    _logger.LogDebug("Skipping {Name} - No collection folder found", item.Name);
                    return false;
                }

                // Check if this library folder is enabled
                var isEnabled = enabledLibraryIds.Contains(rootFolder.Id.ToString());
                
                if (!isEnabled)
                {
                    _logger.LogDebug("Skipping {Name} - Library {LibraryId} not enabled", item.Name, rootFolder.Id);
                }

                return isEnabled;
            }).ToList();

            _logger.LogInformation("Filtering: Found {Total} items total, {Filtered} enabled for processing", allItems.Count, filteredItems.Count);
            return filteredItems;
        }
        /// <summary>
        /// Determine if a video should be skipped based on configuration.
        /// </summary>
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="config">Plugin configuration.</param>
        /// <returns>True if the video should be skipped, false otherwise.</returns>
        private bool ShouldSkipVideo(string videoPath, PluginConfiguration config)
        {
            // Check if video has AI-generated subtitles
            var hasAISubtitles = _subtitleDetectionService.HasAISubtitles(
                videoPath,
                config.TargetLanguage,
                config.AIIdentifier);

            // If has AI subtitles and we're not regenerating, skip
            if (hasAISubtitles && !config.RegenerateAI)
            {
                return true;
            }

            // If has any subtitles and skip existing is enabled, skip
            if (config.SkipExisting && !hasAISubtitles)
            {
                var hasSubtitles = _subtitleDetectionService.HasSubtitles(
                    videoPath,
                    config.TargetLanguage);

                if (hasSubtitles)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
