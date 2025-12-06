using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Service for detecting and managing subtitle files.
    
    public class SubtitleDetectionService : ISubtitleDetectionService
    {
        private readonly ILogger<SubtitleDetectionService> _logger;
        
        // Common subtitle file extensions
        private static readonly string[] SubtitleExtensions = 
        {
            ".srt", ".vtt", ".ass", ".ssa", ".sub", ".idx"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleDetectionService"/> class.
        /// <param name="logger">Logger instance.</param>
        public SubtitleDetectionService(ILogger<SubtitleDetectionService> logger)
        {
            _logger = logger;
        }

        
        public bool HasSubtitles(string videoPath, string language, string? aiIdentifier = null)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                _logger.LogWarning("Video file not found: {VideoPath}", videoPath);
                return false;
            }

            var videoDir = Path.GetDirectoryName(videoPath);
            var videoName = Path.GetFileNameWithoutExtension(videoPath);

            if (string.IsNullOrEmpty(videoDir) || string.IsNullOrEmpty(videoName))
            {
                return false;
            }

            // Check for subtitle files matching the video name and language
            foreach (var ext in SubtitleExtensions)
            {
                // Pattern: video.en.srt, video.en.whisper.srt, etc.
                var patterns = new List<string>
                {
                    $"{videoName}.{language}{ext}",
                    $"{videoName}.{language}.forced{ext}",
                    $"{videoName}.{language}.sdh{ext}"
                };

                // If AI identifier is provided, check for it
                if (!string.IsNullOrEmpty(aiIdentifier))
                {
                    patterns.Add($"{videoName}.{language}.{aiIdentifier}{ext}");
                }

                foreach (var pattern in patterns)
                {
                    var subtitlePath = Path.Combine(videoDir, pattern);
                    if (File.Exists(subtitlePath))
                    {
                        _logger.LogDebug("Found subtitle: {SubtitlePath}", subtitlePath);
                        return true;
                    }
                }
            }

            _logger.LogDebug("No subtitles found for {VideoPath} with language {Language}", videoPath, language);
            return false;
        }

        
        public bool HasAISubtitles(string videoPath, string language, string aiIdentifier)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return false;
            }

            if (string.IsNullOrEmpty(aiIdentifier))
            {
                return false;
            }

            var videoDir = Path.GetDirectoryName(videoPath);
            var videoName = Path.GetFileNameWithoutExtension(videoPath);

            if (string.IsNullOrEmpty(videoDir) || string.IsNullOrEmpty(videoName))
            {
                return false;
            }

            // Check for generated subtitle files
            foreach (var ext in SubtitleExtensions)
            {
                var subtitlePath = Path.Combine(videoDir, $"{videoName}.{language}.{aiIdentifier}{ext}");
                if (File.Exists(subtitlePath))
                {
                    _logger.LogDebug("Found AI subtitle: {SubtitlePath}", subtitlePath);
                    return true;
                }
            }

            return false;
        }

        
        public IEnumerable<string> GetSubtitleFiles(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return Enumerable.Empty<string>();
            }

            var videoDir = Path.GetDirectoryName(videoPath);
            var videoName = Path.GetFileNameWithoutExtension(videoPath);

            if (string.IsNullOrEmpty(videoDir) || string.IsNullOrEmpty(videoName))
            {
                return Enumerable.Empty<string>();
            }

            var subtitleFiles = new List<string>();

            // Find all subtitle files
            foreach (var ext in SubtitleExtensions)
            {
                var pattern = $"{videoName}*{ext}";
                var files = Directory.GetFiles(videoDir, pattern, SearchOption.TopDirectoryOnly);
                subtitleFiles.AddRange(files);
            }

            _logger.LogDebug("Found {Count} subtitle files for {VideoPath}", subtitleFiles.Count, videoPath);
            return subtitleFiles;
        }

        //
        public string GetSubtitlePath(string videoPath, string language, string aiIdentifier, string format = "srt")
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                throw new ArgumentNullException(nameof(videoPath));
            }

            var videoDir = Path.GetDirectoryName(videoPath);
            var videoName = Path.GetFileNameWithoutExtension(videoPath);

            if (string.IsNullOrEmpty(videoDir) || string.IsNullOrEmpty(videoName))
            {
                throw new ArgumentException("Invalid video path", nameof(videoPath));
            }

            // Ensure no leading dot
            format = format.TrimStart('.');

            // Build subtitle filename: video.en.whisper.srt
            var subtitleName = string.IsNullOrEmpty(aiIdentifier)
                ? $"{videoName}.{language}.{format}"
                : $"{videoName}.{language}.{aiIdentifier}.{format}";

            var subtitlePath = Path.Combine(videoDir, subtitleName);

            // Handle long filenames limit 255 characters
            var fileName = Path.GetFileName(subtitlePath);
            if (fileName.Length > 255)
            {
                _logger.LogWarning("Subtitle filename too long ({Length} chars), truncating: {FileName}", 
                    fileName.Length, fileName);

                // Truncate the video name portion
                var maxVideoNameLength = 255 - language.Length - aiIdentifier.Length - format.Length - 4; // dots and extension
                if (maxVideoNameLength > 0)
                {
                    videoName = videoName.Substring(0, Math.Min(videoName.Length, maxVideoNameLength)) + "...";
                    subtitleName = string.IsNullOrEmpty(aiIdentifier)
                        ? $"{videoName}.{language}.{format}"
                        : $"{videoName}.{language}.{aiIdentifier}.{format}";
                    subtitlePath = Path.Combine(videoDir, subtitleName);
                }
            }

            _logger.LogDebug("Generated subtitle path: {SubtitlePath}", subtitlePath);
            return subtitlePath;
        }
    }
}
