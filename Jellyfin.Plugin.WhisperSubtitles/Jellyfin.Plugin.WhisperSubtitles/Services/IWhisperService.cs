using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Interface for Whisper subtitle generation service.
    /// </summary>
    public interface IWhisperService : IDisposable
    {
        /// <summary>
        /// Generate subtitles for a video file.
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="outputPath">Path where the subtitle file should be saved.</param>
        /// <param name="model">Whisper model to use (tiny, base, small, medium, large, turbo).</param>
        /// <param name="language">Target language code (e.g., "en", "ja", "es").</param>
        /// <param name="translate">Whether to translate to English.</param>
        /// <param name="wordTimestamps">Whether to include word-level timestamps.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> GenerateSubtitleAsync(
            string videoPath,
            string outputPath,
            string model,
            string language,
            bool translate,
            bool wordTimestamps,
            CancellationToken cancellationToken);

        /// <summary>
        /// Is a Whisper model available locally.
        /// </summary>
        /// <param name="modelName">Name of the model to check.</param>
        /// <returns>True if model is available, false otherwise.</returns>
        bool IsModelAvailable(string modelName);

        /// <summary>
        /// Download Whisper model if not available.
        /// </summary>
        /// <param name="modelName">Name of the model to download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DownloadModelAsync(string modelName, CancellationToken cancellationToken);
    }
}
