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

namespace Jellyfin.Plugin.WhisperSubtitles.Controllers
{
    /// <summary>
    /// Whisper Subtitles API controller.
    /// </summary>
    [ApiController]
    [Route("WhisperSubtitles")]
    public class WhisperSubtitlesController : ControllerBase
    {
        private readonly ILogger<WhisperSubtitlesController> _logger;
        private readonly IWhisperService _whisperService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperSubtitlesController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{WhisperSubtitlesController}"/> interface.</param>
        /// <param name="whisperService">Instance of the <see cref="IWhisperService"/> interface.</param>
        public WhisperSubtitlesController(ILogger<WhisperSubtitlesController> logger, IWhisperService whisperService)
        {
            _logger = logger;
            _whisperService = whisperService;
            _logger.LogInformation("WhisperSubtitlesController initialized");
        }

        /// <summary>
        /// Test endpoint to verify controller is working.
        /// </summary>
        /// <returns>Test message.</returns>
        [HttpGet("Test")]
        [AllowAnonymous]
        public ActionResult<string> Test()
        {
            _logger.LogInformation("Test endpoint called");
            return Ok("WhisperSubtitles controller is working!");
        }

        /// <summary>
        /// Downloads a Whisper model.
        /// </summary>
        /// <param name="request">The download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Download result.</returns>
        [HttpPost("DownloadModel")]
        [Authorize(Policy = "RequiresElevation")]
        public async Task<ActionResult<ModelDownloadResponse>> DownloadModel(
            [FromBody] ModelDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== DownloadModel CALLED ===");
            _logger.LogInformation("Request received: {Request}", request);
            
            if (request == null)
            {
                _logger.LogError("Request is null");
                return BadRequest("Request is null");
            }
            
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                _logger.LogError("Model name is empty or null");
                return BadRequest("Model name is required");
            }

            _logger.LogInformation("Starting download of Whisper model: {ModelName}", request.ModelName);

            try
            {
                // Validate model name
                var validModels = new[] { "tiny", "tiny.en", "base", "base.en", "small", "small.en", 
                                         "medium", "medium.en", "large", "large-v1", "large-v2", 
                                         "large-v3", "turbo" };
                
                if (!Array.Exists(validModels, m => m.Equals(request.ModelName, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest($"Invalid model name: {request.ModelName}");
                }

                // Download the model using WhisperService
                _logger.LogInformation("Downloading model {Model}", request.ModelName);
                
                var success = await _whisperService.DownloadModelAsync(request.ModelName, cancellationToken);
                
                if (!success)
                {
                    return StatusCode(500, new ModelDownloadResponse
                    {
                        Success = false,
                        Message = $"Failed to download model '{request.ModelName}'"
                    });
                }

                _logger.LogInformation("Model downloaded successfully: {ModelName}", request.ModelName);

                return Ok(new ModelDownloadResponse
                {
                    Success = true,
                    Message = $"Model '{request.ModelName}' downloaded successfully"
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
