using System;
using System.ComponentModel.DataAnnotations;
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
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WhisperSubtitlesController : ControllerBase
    {
        private readonly ILogger<WhisperSubtitlesController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperSubtitlesController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{WhisperSubtitlesController}"/> interface.</param>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        public WhisperSubtitlesController(
            ILogger<WhisperSubtitlesController> logger,
            ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
            _logger.LogInformation("=== DownloadModel API Called ===");
            _logger.LogInformation("Request: {@Request}", request);
            
            if (request == null)
            {
                _logger.LogError("Request is null");
                return BadRequest(new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Request is null"
                });
            }
            
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                _logger.LogError("Model name is empty or null");
                return BadRequest(new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Model name is required"
                });
            }

            _logger.LogInformation("Starting download of Whisper model: {ModelName}", request.ModelName);

            try
            {
                // Create service instance (Jellyfin 10.11 doesn't support plugin DI)
                using var whisperService = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
                
                // Validate model name
                var validModels = new[] { "Tiny", "Base", "Small", "Medium", "Turbo", "Large" };
                
                if (!Array.Exists(validModels, m => m.Equals(request.ModelName, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Invalid model name: {ModelName}", request.ModelName);
                    return BadRequest(new ModelDownloadResponse
                    {
                        Success = false,
                        Message = $"Invalid model name: {request.ModelName}. Valid models: {string.Join(", ", validModels)}"
                    });
                }

                // First ensure whisper.cpp binary is available
                _logger.LogInformation("Checking whisper.cpp binary availability...");
                
                // This will trigger binary build/download if not present
                var binaryReady = await EnsureBinaryAvailableAsync(whisperService, cancellationToken);
                if (!binaryReady)
                {
                    return StatusCode(500, new ModelDownloadResponse
                    {
                        Success = false,
                        Message = "Failed to install whisper.cpp binary. Check logs for details."
                    });
                }

                // Now download the model
                _logger.LogInformation("Downloading model: {Model}", request.ModelName);
                
                var success = await whisperService.DownloadModelAsync(
                    request.ModelName.ToLowerInvariant(), 
                    cancellationToken);
                
                if (!success)
                {
                    _logger.LogError("Model download failed: {ModelName}", request.ModelName);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading model: {ModelName}", request.ModelName);
                return StatusCode(500, new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Failed to download model. Check server logs for details."
                });
            }
        }

        /// <summary>
        /// Install/build whisper.cpp binary from the configuration page.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Installation result with GPU type and binary path.</returns>
        [HttpPost("InstallBinary")]
        [Authorize(Policy = "RequiresElevation")]
        public async Task<ActionResult<BinaryInstallResponse>> InstallBinary(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== InstallBinary API Called ===");
            
            try
            {
                using var whisperService = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());
                
                _logger.LogInformation("Checking whisper.cpp binary availability...");
                
                var binaryReady = await EnsureBinaryAvailableAsync(whisperService, cancellationToken);
                if (!binaryReady)
                {
                    _logger.LogError("Failed to ensure binary availability");
                    return StatusCode(500, new BinaryInstallResponse
                    {
                        Success = false,
                        Message = "Failed to install whisper.cpp binary. Check logs for details."
                    });
                }

                _logger.LogInformation("Binary installation completed successfully");

                return Ok(new BinaryInstallResponse
                {
                    Success = true,
                    Message = "Whisper.cpp binary installed successfully",
                    BinaryPath = whisperService.BinaryPath,
                    GpuType = whisperService.DetectedGpuType
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Binary installation cancelled");
                return StatusCode(499, new BinaryInstallResponse
                {
                    Success = false,
                    Message = "Installation cancelled"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing binary");
                return StatusCode(500, new BinaryInstallResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Ensure whisper.cpp binary is available (build/download if needed).
        /// </summary>
        private async Task<bool> EnsureBinaryAvailableAsync(WhisperService whisperService, CancellationToken cancellationToken)
        {
            // Use reflection to access private EnsureBinaryAvailableAsync method
            // This is a workaround since we can't change IWhisperService interface
            var method = typeof(WhisperService).GetMethod(
                "EnsureBinaryAvailableAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(CancellationToken) },
                null);
            
            if (method == null)
            {
                _logger.LogError("Could not find EnsureBinaryAvailableAsync method via reflection");
                return false;
            }

            try
            {
                var task = method.Invoke(whisperService, new object[] { cancellationToken }) as Task<bool>;
                if (task != null)
                {
                    return await task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking EnsureBinaryAvailableAsync via reflection");
            }

            return false;
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

    /// <summary>
    /// Binary installation response.
    /// </summary>
    public class BinaryInstallResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the installation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the binary path.
        /// </summary>
        public string? BinaryPath { get; set; }

        /// <summary>
        /// Gets or sets the detected GPU type (e.g., "cuda", "vulkan", "metal", or null for CPU).
        /// </summary>
        public string? GpuType { get; set; }
    }
}
