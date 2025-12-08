using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Service for generating subtitles using whisper.cpp binary.
    /// </summary>
    public class WhisperService : IWhisperService
    {
        private readonly ILogger<WhisperService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _modelPath;
        private readonly WhisperBinaryManager _binaryManager;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public WhisperService(ILogger<WhisperService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _binaryManager = new WhisperBinaryManager(logger);
            
            // Model storage path
            var cacheDir = Environment.GetEnvironmentVariable("JELLYFIN_CACHE_DIR");
            if (string.IsNullOrEmpty(cacheDir))
            {
                var homeDir = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(homeDir))
                {
                    homeDir = Path.GetTempPath();
                    _logger.LogWarning("HOME environment variable not set, using temp directory: {TempPath}", homeDir);
                }
                cacheDir = Path.Combine(homeDir, ".cache");
            }
            
            _modelPath = Path.Combine(cacheDir, "whisper");
            
            _logger.LogInformation("WhisperService initialized");
            _logger.LogInformation("Model path: {ModelPath}", _modelPath);
            
            try
            {
                if (!Directory.Exists(_modelPath))
                {
                    Directory.CreateDirectory(_modelPath);
                    _logger.LogInformation("Created Whisper model directory: {ModelPath}", _modelPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create model directory at {ModelPath}", _modelPath);
            }

            // Check binary availability
            if (!_binaryManager.IsBinaryAvailable())
            {
                _logger.LogWarning("Whisper binary not found. Will attempt to download on first use.");
            }
        }

        /// <summary>
        /// Ensure whisper.cpp binary is available, downloading if necessary.
        /// </summary>
        private async Task<bool> EnsureBinaryAvailableAsync(CancellationToken cancellationToken)
        {
            if (_binaryManager.IsBinaryAvailable())
            {
                return true;
            }

            _logger.LogInformation("Whisper binary not found, attempting to download...");
            var success = await _binaryManager.DownloadBinaryAsync(cancellationToken);

            if (success)
            {
                // Test the binary
                var testSuccess = await _binaryManager.TestBinaryAsync(cancellationToken);
                if (!testSuccess)
                {
                    _logger.LogError("Downloaded binary failed test");
                    return false;
                }

                _logger.LogInformation("Whisper binary downloaded and verified successfully");
                return true;
            }

            _logger.LogError("Failed to download whisper binary");
            return false;
        }

        /// <summary>
        /// Generate subtitles for a video file using whisper.cpp.
        /// </summary>
        public async Task<bool> GenerateSubtitleAsync(
            string videoPath,
            string outputPath,
            string model,
            string language,
            bool translate,
            bool wordTimestamps,
            CancellationToken cancellationToken)
        {
            // Get configuration to check GPU setting
            var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
            
            // Ensure binary is available
            if (!await EnsureBinaryAvailableAsync(cancellationToken))
            {
                _logger.LogError("Cannot generate subtitles: whisper.cpp binary not available");
                return false;
            }

            if (!File.Exists(videoPath))
            {
                _logger.LogError("Video file not found: {VideoPath}", videoPath);
                return false;
            }

            try
            {
                // Ensure model is downloaded
                var modelFile = await EnsureModelDownloadedAsync(model, cancellationToken);
                if (string.IsNullOrEmpty(modelFile))
                {
                    _logger.LogError("Failed to get model file for {Model}", model);
                    return false;
                }

                // Log GPU status
                var gpuStatus = config.UseGPUAcceleration && !string.IsNullOrEmpty(_binaryManager.DetectedGPUType) 
                    ? $"GPU ({_binaryManager.DetectedGPUType})" 
                    : "CPU";
                
                _logger.LogInformation(
                    "Generating subtitles: video={Video}, model={Model}, lang={Lang}, translate={Translate}, acceleration={Acceleration}",
                    videoPath, model, language, translate, gpuStatus);

                // Build whisper.cpp command
                var args = new StringBuilder();
                args.Append($"-m \"{modelFile}\" ");
                args.Append($"-f \"{videoPath}\" ");
                args.Append($"-l {language} ");
                args.Append("-osrt ");  // Output SRT format
                args.Append($"-of \"{Path.GetFileNameWithoutExtension(outputPath)}\" ");
                args.Append($"--output-dir \"{Path.GetDirectoryName(outputPath) ?? "."}\" ");

                if (translate)
                {
                    args.Append("-tr ");  // Translate to English
                }

                if (wordTimestamps)
                {
                    args.Append("-ml 1 ");  // Max line length for word timestamps
                }

                // Thread count - use all cores but cap at 16 for efficiency
                var threads = Math.Min(Environment.ProcessorCount, 16);
                args.Append($"-t {threads} ");
                
                // GPU acceleration flag
                if (config.UseGPUAcceleration && !string.IsNullOrEmpty(_binaryManager.DetectedGPUType))
                {
                    switch (_binaryManager.DetectedGPUType)
                    {
                        case "cuda":
                            args.Append("-ngl 999 ");  // Offload all layers to GPU
                            _logger.LogInformation("CUDA acceleration enabled");
                            break;
                        case "vulkan":
                            args.Append("-ngl 999 ");  // Offload all layers to GPU
                            _logger.LogInformation("Vulkan acceleration enabled");
                            break;
                        case "metal":
                            args.Append("-ngl 1 ");    // Metal GPU offloading
                            _logger.LogInformation("Metal acceleration enabled");
                            break;
                    }
                }
                else
                {
                    _logger.LogInformation("Using CPU-only processing");
                }
                
                args.Append("-vv ");  // Verbose output

                var arguments = args.ToString();
                
                _logger.LogInformation("Running: {Binary} {Args}", _binaryManager.BinaryPath, arguments);

                var processInfo = new ProcessStartInfo
                {
                    FileName = _binaryManager.BinaryPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory()
                };
                
                // Set FFmpeg path if available
                if (!string.IsNullOrEmpty(_binaryManager.JellyfinFFmpegPath))
                {
                    var ffmpegDir = Path.GetDirectoryName(_binaryManager.JellyfinFFmpegPath);
                    if (!string.IsNullOrEmpty(ffmpegDir))
                    {
                        processInfo.EnvironmentVariables["PATH"] = $"{ffmpegDir}:{Environment.GetEnvironmentVariable("PATH")}";
                        _logger.LogDebug("Added Jellyfin FFmpeg to PATH: {FFmpegDir}", ffmpegDir);
                    }
                }

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start whisper process");
                    return false;
                }

                var output = new StringBuilder();
                var errors = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        
                        // Log GPU initialization messages
                        if (e.Data.Contains("CUDA") || e.Data.Contains("GPU") || e.Data.Contains("Vulkan"))
                        {
                            _logger.LogInformation("Whisper GPU: {Output}", e.Data);
                        }
                        else
                        {
                            _logger.LogDebug("Whisper: {Output}", e.Data);
                        }
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errors.AppendLine(e.Data);
                        
                        // Log important messages at info level
                        if (e.Data.Contains("progress") || e.Data.Contains("processing") || 
                            e.Data.Contains("CUDA") || e.Data.Contains("GPU"))
                        {
                            _logger.LogInformation("Whisper: {Output}", e.Data);
                        }
                        else
                        {
                            _logger.LogDebug("Whisper: {Output}", e.Data);
                        }
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    _logger.LogError(
                        "Whisper failed with exit code {Code}. Error: {Error}",
                        process.ExitCode,
                        errors.ToString());
                    return false;
                }

                // Verify output file was created
                if (!File.Exists(outputPath))
                {
                    _logger.LogError("Subtitle file was not created: {OutputPath}", outputPath);
                    return false;
                }

                var fileInfo = new FileInfo(outputPath);
                _logger.LogInformation("Successfully generated subtitles: {OutputPath} ({Size} bytes)", 
                    outputPath, fileInfo.Length);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Subtitle generation cancelled for {VideoPath}", videoPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating subtitles for {VideoPath}", videoPath);
                return false;
            }
        }

        /// <summary>
        /// Check if a model is available locally.
        /// </summary>
        public bool IsModelAvailable(string modelName)
        {
            var modelFile = GetModelPath(modelName);
            var exists = File.Exists(modelFile);
            
            _logger.LogDebug("Model {ModelName} availability: {Exists} at {Path}", 
                modelName, exists, modelFile);
            
            return exists;
        }

        /// <summary>
        /// Download a Whisper model if not already available.
        /// </summary>
        public async Task<bool> DownloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            try
            {
                var modelFile = GetModelPath(modelName);
                
                if (File.Exists(modelFile))
                {
                    _logger.LogInformation("Model {Model} already exists at {Path}", modelName, modelFile);
                    return true;
                }

                _logger.LogInformation("Downloading model {Model}", modelName);

                // Download from Hugging Face
                var modelUrl = GetModelDownloadUrl(modelName);
                if (string.IsNullOrEmpty(modelUrl))
                {
                    _logger.LogError("Unknown model: {Model}", modelName);
                    return false;
                }

                using var response = await _httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                _logger.LogInformation("Downloading {Size} MB", totalBytes / 1024 / 1024);

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(modelFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[65536];  // 64KB buffer
                long totalRead = 0;
                int bytesRead;
                var lastLogTime = DateTime.UtcNow;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    // Log progress every 30 seconds
                    if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 30)
                    {
                        var progress = totalBytes > 0 ? (totalRead * 100.0 / totalBytes) : 0;
                        _logger.LogInformation("Download progress: {Progress:F1}% ({Downloaded}/{Total} MB)", 
                            progress, totalRead / 1024 / 1024, totalBytes / 1024 / 1024);
                        lastLogTime = DateTime.UtcNow;
                    }
                }

                _logger.LogInformation("Model {Model} downloaded successfully to {Path}", modelName, modelFile);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download model {Model}", modelName);
                return false;
            }
        }

        /// <summary>
        /// Ensure a model is downloaded before use.
        /// </summary>
        private async Task<string?> EnsureModelDownloadedAsync(string modelName, CancellationToken cancellationToken)
        {
            var modelFile = GetModelPath(modelName);

            if (!File.Exists(modelFile))
            {
                var success = await DownloadModelAsync(modelName, cancellationToken);
                if (!success)
                {
                    return null;
                }
            }

            return modelFile;
        }

        /// <summary>
        /// Get the full path to a model file.
        /// </summary>
        private string GetModelPath(string modelName)
        {
            var fileName = modelName.ToLowerInvariant() switch
            {
                "tiny" => "ggml-tiny.bin",
                "tiny.en" => "ggml-tiny.en.bin",
                "base" => "ggml-base.bin",
                "base.en" => "ggml-base.en.bin",
                "small" => "ggml-small.bin",
                "small.en" => "ggml-small.en.bin",
                "medium" => "ggml-medium.bin",
                "medium.en" => "ggml-medium.en.bin",
                "large" => "ggml-large-v3.bin",
                "large-v1" => "ggml-large-v1.bin",
                "large-v2" => "ggml-large-v2.bin",
                "large-v3" => "ggml-large-v3.bin",
                "turbo" => "ggml-large-v3-turbo.bin",
                _ => $"ggml-{modelName}.bin"
            };

            return Path.Combine(_modelPath, fileName);
        }

        /// <summary>
        /// Get the Hugging Face download URL for a model.
        /// </summary>
        private string? GetModelDownloadUrl(string modelName)
        {
            var baseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";
            
            return modelName.ToLowerInvariant() switch
            {
                "tiny" => $"{baseUrl}/ggml-tiny.bin",
                "tiny.en" => $"{baseUrl}/ggml-tiny.en.bin",
                "base" => $"{baseUrl}/ggml-base.bin",
                "base.en" => $"{baseUrl}/ggml-base.en.bin",
                "small" => $"{baseUrl}/ggml-small.bin",
                "small.en" => $"{baseUrl}/ggml-small.en.bin",
                "medium" => $"{baseUrl}/ggml-medium.bin",
                "medium.en" => $"{baseUrl}/ggml-medium.en.bin",
                "large" => $"{baseUrl}/ggml-large-v3.bin",
                "large-v1" => $"{baseUrl}/ggml-large-v1.bin",
                "large-v2" => $"{baseUrl}/ggml-large-v2.bin",
                "large-v3" => $"{baseUrl}/ggml-large-v3.bin",
                "turbo" => $"{baseUrl}/ggml-large-v3-turbo.bin",
                _ => null
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _disposed = true;
        }
    }
}