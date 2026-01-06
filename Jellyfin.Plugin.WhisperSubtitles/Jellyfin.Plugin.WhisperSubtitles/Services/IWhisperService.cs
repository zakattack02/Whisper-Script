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
    }
}
