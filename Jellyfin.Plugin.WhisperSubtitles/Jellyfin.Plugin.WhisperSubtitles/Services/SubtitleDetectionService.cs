using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Service for detecting and managing subtitle files.
    /// </summary>
    public class SubtitleDetectionService : ISubtitleDetectionService
    {
        private readonly ILogger<SubtitleDetectionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleDetectionService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public SubtitleDetectionService(ILogger<SubtitleDetectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool HasSubtitles(string videoPath, string language, string? aiIdentifier = null)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return false;
            }

            var subtitleFiles = GetSubtitleFiles(videoPath).ToList();
            
            if (aiIdentifier != null)
            {
                // Look for AI-specific subtitles
                return subtitleFiles.Any(f => f.Contains(language) && f.Contains(aiIdentifier));
            }

            // Look for any subtitles in the language
            return subtitleFiles.Any(f => f.Contains(language));
        }

        /// <inheritdoc />
        public bool HasAISubtitles(string videoPath, string language, string aiIdentifier)
        {
            if (string.IsNullOrEmpty(videoPath) || string.IsNullOrEmpty(aiIdentifier))
            {
                return false;
            }

            return HasSubtitles(videoPath, language, aiIdentifier);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetSubtitleFiles(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return Enumerable.Empty<string>();
            }

            var directory = Path.GetDirectoryName(videoPath);
            if (string.IsNullOrEmpty(directory))
            {
                return Enumerable.Empty<string>();
            }

            var fileName = Path.GetFileNameWithoutExtension(videoPath);
            var subtitleExtensions = new[] { ".srt", ".vtt", ".ass", ".ssa", ".sub" };

            try
            {
                var subtitleFiles = Directory.GetFiles(directory, fileName + "*")
                    .Where(f => subtitleExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                return subtitleFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subtitle files for {VideoPath}", videoPath);
                return Enumerable.Empty<string>();
            }
        }

        /// <inheritdoc />
        public string GetSubtitlePath(string videoPath, string language, string aiIdentifier, string format = "srt")
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                throw new ArgumentNullException(nameof(videoPath));
            }

            var directory = Path.GetDirectoryName(videoPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoPath);

            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            // Generate subtitle filename: video.en.whisper.srt
            var subtitleFileName = string.IsNullOrEmpty(aiIdentifier)
                ? $"{fileNameWithoutExtension}.{language}.{format}"
                : $"{fileNameWithoutExtension}.{language}.{aiIdentifier}.{format}";

            return Path.Combine(directory, subtitleFileName);
        }
    }
}
