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
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WhisperSubtitlesController : ControllerBase
    {
        private readonly ILogger<WhisperSubtitlesController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public WhisperSubtitlesController(
            ILogger<WhisperSubtitlesController> logger,
            ILoggerFactory loggerFactory)
        {
            _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger.LogInformation("WhisperSubtitlesController initialized");
        }

        [HttpGet("Test")]
        [AllowAnonymous]
        public ActionResult<string> Test()
        {
            _logger.LogInformation("Test endpoint called");
            return Ok("WhisperSubtitles controller is working!");
        }

        /// <summary>Downloads (deploys from bundle) the requested Whisper model.</summary>
        [HttpPost("DownloadModel")]
        [Authorize(Policy = "RequiresElevation")]
        public async Task<ActionResult<ModelDownloadResponse>> DownloadModel(
            [FromBody] ModelDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("DownloadModel called: {@Request}", request);

            if (request is null || string.IsNullOrWhiteSpace(request.ModelName))
            {
                return BadRequest(new ModelDownloadResponse
                {
                    Success = false,
                    Message = "ModelName is required"
                });
            }

            var validModels = new[] { "Tiny", "Base", "Small", "Medium", "Turbo", "Large" };
            if (!Array.Exists(validModels,
                    m => m.Equals(request.ModelName, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new ModelDownloadResponse
                {
                    Success = false,
                    Message = $"Invalid model '{request.ModelName}'. Valid: {string.Join(", ", validModels)}"
                });
            }

            try
            {
                using var svc = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());

                // Ensure binary is in place before downloading the model
                if (!await EnsureBinaryAsync(svc, cancellationToken))
                {
                    return StatusCode(500, new ModelDownloadResponse
                    {
                        Success = false,
                        Message = "Failed to deploy whisper binary. Check server logs."
                    });
                }

                var ok = await svc.DownloadModelAsync(
                    request.ModelName.ToLowerInvariant(), cancellationToken);

                if (!ok)
                {
                    return StatusCode(500, new ModelDownloadResponse
                    {
                        Success = false,
                        Message = $"Failed to download model '{request.ModelName}'"
                    });
                }

                return Ok(new ModelDownloadResponse
                {
                    Success = true,
                    Message = $"Model '{request.ModelName}' downloaded successfully"
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Download cancelled"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading model {Model}", request.ModelName);
                return StatusCode(500, new ModelDownloadResponse
                {
                    Success = false,
                    Message = "Download failed. Check server logs."
                });
            }
        }

        /// <summary>Checks if the whisper binary is available and ready to use.</summary>
        [HttpGet("BinaryStatus")]
        [AllowAnonymous]
        public ActionResult<BinaryStatusResponse> BinaryStatus()
        {
            _logger.LogInformation("BinaryStatus called");

            try
            {
                using var svc = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());

                var isReady = svc.IsBinaryAvailable();

                return Ok(new BinaryStatusResponse
                {
                    IsReady    = isReady,
                    BinaryPath = svc.BinaryPath,
                    GpuType    = svc.DetectedGpuType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking binary status");
                return Ok(new BinaryStatusResponse
                {
                    IsReady = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>Deploys the bundled whisper binary from the plugin to the cache.</summary>
        [HttpPost("InstallBinary")]
        [Authorize(Policy = "RequiresElevation")]
        public async Task<ActionResult<BinaryInstallResponse>> InstallBinary(
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("InstallBinary called");

            try
            {
                using var svc = new WhisperService(_loggerFactory.CreateLogger<WhisperService>());

                if (!await EnsureBinaryAsync(svc, cancellationToken))
                {
                    return StatusCode(500, new BinaryInstallResponse
                    {
                        Success = false,
                        Message = "Failed to deploy whisper binary. Check server logs."
                    });
                }

                return Ok(new BinaryInstallResponse
                {
                    Success    = true,
                    Message    = "whisper binary deployed successfully",
                    BinaryPath = svc.BinaryPath,
                    GpuType    = svc.DetectedGpuType
                });
            }
            catch (OperationCanceledException)
            {
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

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Calls the private EnsureBinaryAvailableAsync on WhisperService via reflection.
        /// This indirection exists because IWhisperService intentionally doesn't expose it.
        /// </summary>
        private async Task<bool> EnsureBinaryAsync(WhisperService svc, CancellationToken ct)
        {
            var method = typeof(WhisperService).GetMethod(
                "EnsureBinaryAvailableAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(CancellationToken) },
                null);

            if (method is null)
            {
                _logger.LogError("EnsureBinaryAvailableAsync not found via reflection");
                return false;
            }

            try
            {
                var task = method.Invoke(svc, new object[] { ct }) as Task<bool>;
                return task is not null && await task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reflection call to EnsureBinaryAvailableAsync failed");
                return false;
            }
        }
    }

    public class ModelDownloadRequest
    {
        [Required]
        public string ModelName { get; set; } = string.Empty;
    }

    public class ModelDownloadResponse
    {
        public bool    Success   { get; set; }
        public string  Message   { get; set; } = string.Empty;
        public string? ModelPath { get; set; }
    }

    public class BinaryStatusResponse
    {
        public bool    IsReady    { get; set; }
        public string  Message    { get; set; } = string.Empty;
        public string? BinaryPath { get; set; }
        public string? GpuType    { get; set; }
    }

    public class BinaryInstallResponse
    {
        public bool    Success    { get; set; }
        public string  Message    { get; set; } = string.Empty;
        public string? BinaryPath { get; set; }
        public string? GpuType    { get; set; }
    }
}