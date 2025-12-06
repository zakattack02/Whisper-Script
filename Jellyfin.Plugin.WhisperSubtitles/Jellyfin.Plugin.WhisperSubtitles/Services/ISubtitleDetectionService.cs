using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Interface for subtitle detection service.
    
    public interface ISubtitleDetectionService
    {
        /// <summary>
        /// Check if a video file has subtitles for a specific language.
        
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="language">Language code (e.g., "en", "ja").</param>
        /// <param name="aiIdentifier">AI identifier to look for (e.g., "whisper").</param>
        /// <returns>True if subtitles exist, false otherwise.</returns>
        bool HasSubtitles(string videoPath, string language, string? aiIdentifier = null);

        /// <summary>
        /// Check if a video file has AI-generated subtitles.
        
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="language">Language code.</param>
        /// <param name="aiIdentifier">AI identifier to check for.</param>
        /// <returns>True if AI-generated subtitles exist, false otherwise.</returns>
        bool HasAISubtitles(string videoPath, string language, string aiIdentifier);

        /// <summary>
        /// Get all subtitle files for a video.
       
        /// <param name="videoPath">Path to the video file.</param>
        /// <returns>List of subtitle file paths.</returns>
        IEnumerable<string> GetSubtitleFiles(string videoPath);

        /// <summary>
        /// Generate the subtitle output path for a video file.
       
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="language">Language code.</param>
        /// <param name="aiIdentifier">AI identifier to include in filename.</param>
        /// <param name="format">Subtitle format (e.g., "srt", "vtt").</param>
        /// <returns>Full path for the subtitle file.</returns>
        string GetSubtitlePath(string videoPath, string language, string aiIdentifier, string format = "srt");
    }
}
