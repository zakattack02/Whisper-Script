using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Interface for Whisper subtitle service.
    /// </summary>
    public interface IWhisperService
    {
        /// <summary>
        /// Download a Whisper model.
        /// </summary>
        /// <param name="modelName">Name of the model to download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DownloadModelAsync(string modelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate subtitles for a video file.
        /// </summary>
        /// <param name="videoPath">Path to the video file.</param>
        /// <param name="subtitlePath">Path where subtitle file should be saved.</param>
        /// <param name="modelName">Whisper model to use.</param>
        /// <param name="language">Target language code.</param>
        /// <param name="translate">Whether to translate to English.</param>
        /// <param name="wordTimestamps">Whether to include word-level timestamps.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> GenerateSubtitleAsync(
            string videoPath,
            string subtitlePath,
            string modelName,
            string language,
            bool translate,
            bool wordTimestamps,
            CancellationToken cancellationToken = default);
    }
}
