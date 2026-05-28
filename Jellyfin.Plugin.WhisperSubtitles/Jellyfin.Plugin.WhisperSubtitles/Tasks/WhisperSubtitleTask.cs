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
    /// Scheduled task — generates subtitles for videos selected in the plugin config.
    /// </summary>
    public class WhisperSubtitleTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WhisperSubtitleTask> _logger;
        private readonly ILocalizationManager _localization;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWhisperService _whisperService;
        private readonly ISubtitleDetectionService _subtitleDetectionService;

        public WhisperSubtitleTask(
            ILibraryManager libraryManager,
            ILogger<WhisperSubtitleTask> logger,
            ILocalizationManager localization,
            ILoggerFactory loggerFactory)
        {
            _libraryManager  = libraryManager;
            _logger          = logger;
            _localization    = localization;
            _loggerFactory   = loggerFactory;

            _whisperService           = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
            _subtitleDetectionService = new SubtitleDetectionService(_loggerFactory.CreateLogger<SubtitleDetectionService>());
        }

        public string Name        => "Generate Whisper Subtitles";
        public string Key         => "WhisperSubtitleGeneration";
        public string Description => "Generates AI-powered subtitles for videos using OpenAI Whisper";
        public string Category    => _localization.GetLocalizedString("TasksLibraryCategory");

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
            Array.Empty<TaskTriggerInfo>();

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

            _logger.LogInformation(
                "Whisper task starting. Model={M}, Language={L}, Translate={T}, Identifier={I}",
                config.WhisperModel, config.TargetLanguage, config.TranslateToEnglish, config.AIIdentifier);

            var videos = GetVideoItems(config);

            if (videos.Count == 0)
            {
                _logger.LogInformation("No videos to process — check library selection in plugin settings.");
                progress?.Report(100);
                return;
            }

            _logger.LogInformation("Processing {Count} video(s)", videos.Count);

            var videoCount = videos.Count;
            int processed = 0, skipped = 0, errors = 0;
            double videoWeight = videoCount > 0 ? 100.0 / videoCount : 0;

            foreach (var video in videos)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Task cancelled by user");
                    break;
                }

                try
                {
                    var videoPath = video.Path;

                    if (ShouldSkip(videoPath, config))
                    {
                        _logger.LogDebug("Skipping (has subtitles): {Path}", videoPath);
                        skipped++;
                        processed++;
                        progress?.Report((double)processed / videoCount * 100);
                        continue;
                    }

                    var subtitlePath = _subtitleDetectionService.GetSubtitlePath(
                        videoPath, config.TargetLanguage, config.AIIdentifier, "srt");

                    _logger.LogInformation("Generating: {Path}", videoPath);

                    double baseProgress = (double)processed / videoCount * 100;
                    var videoProgress = new Progress<double>(p =>
                    {
                        progress?.Report(baseProgress + p * videoWeight);
                    });

                    var ok = await _whisperService.GenerateSubtitleAsync(
                        videoPath, subtitlePath,
                        config.WhisperModel.ToString(),
                        config.TargetLanguage,
                        config.TranslateToEnglish,
                        config.WordTimestamps,
                        videoProgress,
                        cancellationToken);

                    if (!ok)
                    {
                        _logger.LogError("Failed to generate subtitles: {Path}", videoPath);
                        errors++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing: {Path}", video.Path);
                    errors++;
                }

                processed++;
                progress?.Report((double)processed / videoCount * 100);
            }

            _logger.LogInformation(
                "Task complete. Generated={G}, Skipped={S}, Errors={E}",
                processed - skipped - errors, skipped, errors);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the list of videos to process, filtered by configured libraries.
        /// Includes Movie, Episode, AND Video (home videos / generic video files).
        /// </summary>
        private List<BaseItem> GetVideoItems(PluginConfiguration config)
        {
            var enabledLibraries = config.LibrariesToProcess ?? new List<string>();

            _logger.LogInformation(
                "Configured library IDs ({Count}): [{Ids}]",
                enabledLibraries.Count,
                string.Join(", ", enabledLibraries));

            // Include Video so "Home Videos" libraries are not silently ignored.
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Video },
                IsVirtualItem    = false,
                Recursive        = true
            };

            var allItems = _libraryManager.GetItemList(query);

            _logger.LogInformation("Library query returned {Count} item(s) before filtering", allItems.Count);

            // If no libraries are configured, process everything.
            if (!enabledLibraries.Any())
            {
                _logger.LogInformation("No library filter configured — processing all {Count} item(s)", allItems.Count);
                return allItems
                    .Where(i => !string.IsNullOrEmpty(i.Path) && File.Exists(i.Path))
                    .ToList();
            }

            // The config page stores ItemIds from getVirtualFolders() which are hex strings
            // WITHOUT hyphens (e.g. "ff6fbd42ce07adfc36b566506eba4f82").
            // Guid.ToString() produces hyphenated format (e.g. "ff6fbd42-ce07-adfc-36b5-66506eba4f82").
            // Normalize both sides to hyphenless lowercase so they always match.
            var normalizedEnabled = new HashSet<string>(
                enabledLibraries
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(NormalizeId),
                StringComparer.OrdinalIgnoreCase);

            _logger.LogDebug("Normalized enabled IDs: [{Ids}]", string.Join(", ", normalizedEnabled));

            var filtered = new List<BaseItem>();

            foreach (var item in allItems)
            {
                if (string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path))
                    continue;

                try
                {
                    // GetCollectionFolders returns the top-level virtual folders (libraries)
                    // that contain this item. Each folder has a stable Guid Id.
                    var parentFolders = _libraryManager.GetCollectionFolders(item);

                    // Check whether any of the item's parent libraries are in our allow-list.
                    // Normalize the folder ID to match the config format.
                    if (parentFolders.Any(f => normalizedEnabled.Contains(NormalizeId(f.Id.ToString()))))
                    {
                        filtered.Add(item);
                        _logger.LogDebug("Item '{Name}' matched library filter", item.Name);
                    }
                    else if (parentFolders.Count > 0)
                    {
                        _logger.LogDebug(
                            "Skipping '{Name}' — parent libraries: [{Folders}]",
                            item.Name,
                            string.Join(", ", parentFolders.Select(f => NormalizeId(f.Id.ToString()))));
                    }
                    else
                    {
                        _logger.LogDebug("Item '{Name}' has no parent collection folders", item.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error resolving library for '{Name}' — skipping", item.Name);
                }
            }

            _logger.LogInformation(
                "{Filtered} of {Total} item(s) match the configured libraries",
                filtered.Count, allItems.Count);

            return filtered;
        }

        /// <summary>
        /// Strips hyphens and lowercases a GUID/ItemId string so that
        /// "ff6fbd42ce07adfc36b566506eba4f82" and "ff6fbd42-ce07-adfc-36b5-66506eba4f82"
        /// both normalize to the same value.
        /// </summary>
        private static string NormalizeId(string id) =>
            id.Replace("-", string.Empty).ToLowerInvariant();

        private bool ShouldSkip(string videoPath, PluginConfiguration config)
        {
            var hasAI = _subtitleDetectionService.HasAISubtitles(
                videoPath, config.TargetLanguage, config.AIIdentifier);

            if (hasAI && !config.RegenerateAI)
                return true;

            if (config.SkipExisting && !hasAI)
            {
                if (_subtitleDetectionService.HasSubtitles(videoPath, config.TargetLanguage))
                    return true;
            }

            return false;
        }
    }
}