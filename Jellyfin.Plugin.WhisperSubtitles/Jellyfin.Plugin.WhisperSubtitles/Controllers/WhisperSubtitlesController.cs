using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Whisper.net.Ggml;

namespace Jellyfin.Plugin.WhisperSubtitles.Controllers
{
    /// <summary>
    /// Whisper Subtitles API controller.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("WhisperSubtitles")]
    public class WhisperSubtitlesController : ControllerBase
    {
        private readonly ILogger<WhisperSubtitlesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperSubtitlesController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{WhisperSubtitlesController}"/> interface.</param>
        public WhisperSubtitlesController(ILogger<WhisperSubtitlesController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Downloads a Whisper model.
        /// </summary>
        /// <param name="request">The download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Download result.</returns>
        [HttpPost("DownloadModel")]
        public async Task<ActionResult<ModelDownloadResponse>> DownloadModel(
            [FromBody] ModelDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                return BadRequest("Model name is required");
            }

            _logger.LogInformation("Starting download of Whisper model: {ModelName}", request.ModelName);

            try
            {
                // Parse the model type
                if (!Enum.TryParse<GgmlType>(request.ModelName, true, out var ggmlType))
                {
                    return BadRequest($"Invalid model name: {request.ModelName}");
                }

                // Get model directory
                var cacheDir = Environment.GetEnvironmentVariable("JELLYFIN_CACHE_DIR");
                if (string.IsNullOrEmpty(cacheDir))
                {
                    var homeDir = Environment.GetEnvironmentVariable("HOME");
                    if (string.IsNullOrEmpty(homeDir))
                    {
                        homeDir = Path.GetTempPath();
                    }
                    cacheDir = Path.Combine(homeDir, ".cache");
                }

                var modelPath = Path.Combine(cacheDir, "whisper");

                // Ensure directory exists
                if (!Directory.Exists(modelPath))
                {
                    Directory.CreateDirectory(modelPath);
                    _logger.LogInformation("Created model directory: {ModelPath}", modelPath);
                }

                // Download the model using WhisperGgmlDownloader
                _logger.LogInformation("Downloading model {Model} to {Path}", ggmlType, modelPath);
                
                using var httpClient = new HttpClient();
                var downloader = new WhisperGgmlDownloader(httpClient);
                await using var modelStream = await downloader.GetGgmlModelAsync(ggmlType, cancellationToken: cancellationToken);
                
                var modelFileName = $"ggml-{request.ModelName.ToLowerInvariant()}.bin";
                var modelFilePath = Path.Combine(modelPath, modelFileName);
                
                // Save the model file
                await using var fileStream = System.IO.File.Create(modelFilePath);
                await modelStream.CopyToAsync(fileStream, cancellationToken);
                
                _logger.LogInformation("Model downloaded successfully: {ModelFile}", modelFilePath);

                return Ok(new ModelDownloadResponse
                {
                    Success = true,
                    Message = $"Model '{request.ModelName}' downloaded successfully",
                    ModelPath = modelFilePath
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Model download cancelled: {ModelName}", request.ModelName);
                return StatusCode(499, new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Download cancelled"
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error downloading model: {ModelName}", request.ModelName);
                return StatusCode(503, new ModelDownloadResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading model: {ModelName}", request.ModelName);
                return StatusCode(500, new ModelDownloadResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }
    }

    /// <summary>
    /// Model download request.
    /// </summary>
    public class ModelDownloadRequest
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        [Required]
        public string ModelName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model download response.
    /// </summary>
    public class ModelDownloadResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the download was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model path.
        /// </summary>
        public string? ModelPath { get; set; }
    }
}
