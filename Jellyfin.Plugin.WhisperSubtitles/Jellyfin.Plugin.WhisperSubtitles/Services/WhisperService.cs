using System;
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
    /// Generates subtitles using the bundled whisper.cpp binary.
    /// </summary>
    public class WhisperService : IWhisperService, IDisposable
    {
        private readonly ILogger<WhisperService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _modelPath;
        private readonly WhisperBinaryManager _binaryManager;
        private bool _disposed;

        public WhisperService(ILogger<WhisperService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();

            // No pluginPath parameter — WhisperBinaryManager uses assembly location now.
            _binaryManager = new WhisperBinaryManager(logger);

            // Model storage
            var cacheDir = Environment.GetEnvironmentVariable("JELLYFIN_CACHE_DIR");
            if (string.IsNullOrEmpty(cacheDir))
            {
                var home = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
                cacheDir = Path.Combine(home, ".cache");
            }

            _modelPath = Path.Combine(cacheDir, "whisper");

            try { Directory.CreateDirectory(_modelPath); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create model dir: {Dir}", _modelPath); }

            _logger.LogInformation("WhisperService ready. Model path: {Path}", _modelPath);

            if (!_binaryManager.IsBinaryAvailable())
                _logger.LogWarning("Whisper binary not in cache — will deploy from plugin bundle on first use.");
        }

        public string  BinaryPath      => _binaryManager.BinaryPath;
        public string? DetectedGpuType => _binaryManager.DetectedGPUType;

        /// <summary>Returns true if the whisper binary is available and ready to use.</summary>
        public bool IsBinaryAvailable() => _binaryManager.IsBinaryAvailable();

        // ── IWhisperService ────────────────────────────────────────────────────

        public async Task<bool> DownloadModelAsync(
            string modelName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var modelFile = GetModelPath(modelName);
                if (File.Exists(modelFile))
                {
                    _logger.LogInformation("Model '{Model}' already cached at {Path}", modelName, modelFile);
                    return true;
                }

                var url = GetModelDownloadUrl(modelName);
                if (url is null)
                {
                    _logger.LogError("Unknown model name: {Model}", modelName);
                    return false;
                }

                _logger.LogInformation("Downloading model '{Model}' from {Url}", modelName, url);

                using var response = await _httpClient.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                _logger.LogInformation("Model size: {MB} MB", totalBytes / 1024 / 1024);

                await using var src  = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var dest = new FileStream(
                    modelFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

                var buffer      = new byte[65536];
                long totalRead  = 0;
                int  bytesRead;
                var  lastLog    = DateTime.UtcNow;

                while ((bytesRead = await src.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await dest.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if ((DateTime.UtcNow - lastLog).TotalSeconds >= 30)
                    {
                        var pct = totalBytes > 0 ? totalRead * 100.0 / totalBytes : 0;
                        _logger.LogInformation("Download: {Pct:F1}% ({Read}/{Total} MB)",
                            pct, totalRead / 1024 / 1024, totalBytes / 1024 / 1024);
                        lastLog = DateTime.UtcNow;
                    }
                }

                _logger.LogInformation("Model '{Model}' downloaded to {Path}", modelName, modelFile);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download model '{Model}'", modelName);
                return false;
            }
        }

        public async Task<bool> GenerateSubtitleAsync(
            string videoPath,
            string outputPath,
            string modelName,
            string language,
            bool   translate,
            bool   wordTimestamps,
            CancellationToken cancellationToken = default)
        {
            var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

            // 1. Ensure binary is ready
            if (!await EnsureBinaryAvailableAsync(cancellationToken))
            {
                _logger.LogError("Whisper binary not available — cannot generate subtitles");
                return false;
            }

            if (!File.Exists(videoPath))
            {
                _logger.LogError("Video not found: {Path}", videoPath);
                return false;
            }

            // 2. Ensure model is downloaded
            var modelFile = await EnsureModelDownloadedAsync(modelName, cancellationToken);
            if (modelFile is null)
            {
                _logger.LogError("Could not obtain model file for '{Model}'", modelName);
                return false;
            }

            try
            {
                var outputDir  = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
                // whisper-cli writes <stem>.srt; we strip the .srt extension from outputPath.
                var outputStem = Path.GetFileNameWithoutExtension(outputPath);

                // ── Build argument list ────────────────────────────────────────
                // IMPORTANT: do NOT wrap the whole string in extra quotes.
                // Each path that may contain spaces must be individually quoted.
                var args = BuildArguments(
                    modelFile, videoPath, outputDir, outputStem,
                    language, translate, wordTimestamps,
                    config.UseGPUAcceleration ? _binaryManager.DetectedGPUType : null);

                var acceleration = config.UseGPUAcceleration && _binaryManager.DetectedGPUType is not null
                    ? $"GPU ({_binaryManager.DetectedGPUType})"
                    : "CPU";

                _logger.LogInformation(
                    "Generating subtitles: video={Video}, model={Model}, lang={Lang}, translate={T}, accel={A}",
                    videoPath, modelName, language, translate, acceleration);
                _logger.LogInformation("Command: {Binary} {Args}", _binaryManager.BinaryPath, args);

                var psi = new ProcessStartInfo
                {
                    FileName               = _binaryManager.BinaryPath,
                    Arguments              = args,          // plain string, no outer quotes
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    WorkingDirectory       = outputDir
                };

                // Prepend Jellyfin's FFmpeg directory to PATH so whisper-cli can find it
                if (!string.IsNullOrEmpty(_binaryManager.JellyfinFFmpegPath))
                {
                    var ffmpegDir = Path.GetDirectoryName(_binaryManager.JellyfinFFmpegPath)!;
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    psi.EnvironmentVariables["PATH"] = $"{ffmpegDir}:{currentPath}";
                }

                using var process = Process.Start(psi);
                if (process is null)
                {
                    _logger.LogError("Failed to start whisper-whisper-cli process");
                    return false;
                }

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                using var outDone = new ManualResetEventSlim(false);
                using var errDone = new ManualResetEventSlim(false);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is null) { outDone.Set(); return; }
                    stdout.AppendLine(e.Data);
                    _logger.LogDebug("whisper: {L}", e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null) { errDone.Set(); return; }
                    stderr.AppendLine(e.Data);
                    // Surface GPU init and progress at Info level; rest is Debug
                    if (e.Data.Contains("CUDA") || e.Data.Contains("GPU") ||
                        e.Data.Contains("progress") || e.Data.Contains("processing"))
                        _logger.LogInformation("whisper: {L}", e.Data);
                    else
                        _logger.LogDebug("whisper: {L}", e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);
                outDone.Wait(5_000);
                errDone.Wait(5_000);

                if (process.ExitCode != 0)
                {
                    _logger.LogError(
                        "whisper-whisper-cli exited {Code}.\nstderr: {Err}\nstdout: {Out}",
                        process.ExitCode,
                        stderr.Length > 0 ? stderr.ToString() : "(empty)",
                        stdout.Length > 0 ? stdout.ToString() : "(empty)");
                    return false;
                }

                if (!File.Exists(outputPath))
                {
                    _logger.LogError("Subtitle file not created: {Path}", outputPath);
                    return false;
                }

                _logger.LogInformation("Subtitles written: {Path} ({Bytes} bytes)",
                    outputPath, new FileInfo(outputPath).Length);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Subtitle generation cancelled for {Video}", videoPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating subtitles for {Video}", videoPath);
                return false;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Constructs the argument string for whisper-whisper-cli.
        /// Each path is individually quoted; the whole string is NOT wrapped in quotes.
        /// </summary>
        private string BuildArguments(
            string modelFile,
            string videoPath,
            string outputDir,
            string outputStem,
            string language,
            bool   translate,
            bool   wordTimestamps,
            string? gpuType)
        {
            var sb = new StringBuilder();

            // Model and input — paths individually quoted
            sb.Append($"-m \"{modelFile}\" ");
            sb.Append($"-f \"{videoPath}\" ");

            // Language
            sb.Append($"-l {language} ");

            // Output — SRT format, stem only (no extension), output directory
            sb.Append("-osrt ");
            sb.Append($"-of \"{outputStem}\" ");
            sb.Append($"--output-dir \"{outputDir}\" ");

            if (translate)
                sb.Append("-tr ");

            if (wordTimestamps)
                sb.Append("-ml 1 ");

            // Thread cap
            var threads = Math.Min(Environment.ProcessorCount, 16);
            sb.Append($"-t {threads} ");

            // GPU offload
            if (gpuType is not null)
            {
                sb.Append("-ngl 999 ");
                _logger.LogInformation("{GPU} acceleration enabled", gpuType);
            }
            else
            {
                _logger.LogInformation("CPU-only processing");
            }

            sb.Append("-vv");

            return sb.ToString();
        }

        private async Task<bool> EnsureBinaryAvailableAsync(CancellationToken cancellationToken)
        {
            if (_binaryManager.IsBinaryAvailable())
                return true;

            _logger.LogInformation("Binary not in cache — deploying from plugin bundle...");
            var deployed = await _binaryManager.DownloadBinaryAsync(cancellationToken);

            if (!deployed)
            {
                _logger.LogError("Failed to deploy whisper binary from plugin bundle");
                return false;
            }

            var tested = await _binaryManager.TestBinaryAsync(cancellationToken);
            if (!tested)
                _logger.LogError("Deployed binary failed self-test");

            return tested;
        }

        private async Task<string?> EnsureModelDownloadedAsync(
            string modelName, CancellationToken cancellationToken)
        {
            var path = GetModelPath(modelName);
            if (File.Exists(path))
                return path;

            var ok = await DownloadModelAsync(modelName, cancellationToken);
            return ok ? path : null;
        }

        public bool IsModelAvailable(string modelName) => File.Exists(GetModelPath(modelName));

        private string GetModelPath(string modelName)
        {
            var file = modelName.ToLowerInvariant() switch
            {
                "tiny"      => "ggml-tiny.bin",
                "tiny.en"   => "ggml-tiny.en.bin",
                "base"      => "ggml-base.bin",
                "base.en"   => "ggml-base.en.bin",
                "small"     => "ggml-small.bin",
                "small.en"  => "ggml-small.en.bin",
                "medium"    => "ggml-medium.bin",
                "medium.en" => "ggml-medium.en.bin",
                "large"     => "ggml-large-v3.bin",
                "large-v1"  => "ggml-large-v1.bin",
                "large-v2"  => "ggml-large-v2.bin",
                "large-v3"  => "ggml-large-v3.bin",
                "turbo"     => "ggml-large-v3-turbo.bin",
                _           => $"ggml-{modelName}.bin"
            };
            return Path.Combine(_modelPath, file);
        }

        private static string? GetModelDownloadUrl(string modelName)
        {
            const string base_url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";
            return modelName.ToLowerInvariant() switch
            {
                "tiny"      => $"{base_url}/ggml-tiny.bin",
                "tiny.en"   => $"{base_url}/ggml-tiny.en.bin",
                "base"      => $"{base_url}/ggml-base.bin",
                "base.en"   => $"{base_url}/ggml-base.en.bin",
                "small"     => $"{base_url}/ggml-small.bin",
                "small.en"  => $"{base_url}/ggml-small.en.bin",
                "medium"    => $"{base_url}/ggml-medium.bin",
                "medium.en" => $"{base_url}/ggml-medium.en.bin",
                "large"     => $"{base_url}/ggml-large-v3.bin",
                "large-v1"  => $"{base_url}/ggml-large-v1.bin",
                "large-v2"  => $"{base_url}/ggml-large-v2.bin",
                "large-v3"  => $"{base_url}/ggml-large-v3.bin",
                "turbo"     => $"{base_url}/ggml-large-v3-turbo.bin",
                _           => null
            };
        }

        // ── IDisposable ────────────────────────────────────────────────────────

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _httpClient?.Dispose();
                _binaryManager?.Dispose();
            }
            _disposed = true;
        }

        ~WhisperService() => Dispose(false);
    }
}