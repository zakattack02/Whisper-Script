using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        private const int ChunkDurationMs = 30 * 60 * 1000; // 30 min per chunk to stay under ~2GB RAM

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
                var outputStem = Path.GetFileNameWithoutExtension(outputPath);

                // ── Extract audio from video ───────────────────────────────────
                var tempWav = Path.Combine(
                    Path.GetTempPath(),
                    $"whisper_{Guid.NewGuid():N}.wav");

                if (!await ExtractAudioAsync(videoPath, tempWav, cancellationToken))
                {
                    _logger.LogError("Failed to extract audio from {Video}", videoPath);
                    return false;
                }

                var gpuType = config.UseGPUAcceleration ? _binaryManager.DetectedGPUType : null;
                var acceleration = gpuType is not null ? $"GPU ({gpuType})" : "CPU";

                _logger.LogInformation(
                    "Generating subtitles: video={Video}, model={Model}, lang={Lang}, translate={T}, accel={A}",
                    videoPath, modelName, language, translate, acceleration);

                var wavDurationMs = await GetWavDurationMsAsync(tempWav, cancellationToken);

                if (wavDurationMs <= ChunkDurationMs)
                {
                    if (!await RunWhisperCli(
                        modelFile, tempWav, outputDir, outputStem,
                        language, translate, wordTimestamps, gpuType, cancellationToken))
                        return false;
                }
                else
                {
                    var chunks = await SplitWavAsync(tempWav, ChunkDurationMs, cancellationToken);
                    try
                    {
                        var mergedSrt = new StringBuilder();
                        int segmentOffset = 0;

                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunkStem = $"{outputStem}.part{i:D3}";

                            if (!await RunWhisperCli(
                                modelFile, chunks[i], outputDir, chunkStem,
                                language, translate, wordTimestamps, gpuType, cancellationToken))
                                return false;

                            var chunkSrtPath = Path.Combine(outputDir, $"{chunkStem}.srt");
                            segmentOffset = MergeSrtInto(chunkSrtPath, mergedSrt, segmentOffset);
                            File.Delete(chunkSrtPath);
                        }

                        await File.WriteAllTextAsync(outputPath, mergedSrt.ToString(), cancellationToken);
                    }
                    finally
                    {
                        foreach (var cp in chunks)
                        {
                            try { if (File.Exists(cp)) File.Delete(cp); }
                            catch { }
                        }
                    }
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

        private async Task<bool> RunWhisperCli(
            string modelFile, string wavPath, string outputDir, string outputStem,
            string language, bool translate, bool wordTimestamps, string? gpuType,
            CancellationToken cancellationToken)
        {
            var args = BuildArguments(
                modelFile, wavPath, outputDir, outputStem,
                language, translate, wordTimestamps, gpuType);

            _logger.LogInformation("Command: {Binary} {Args}", _binaryManager.BinaryPath, args);

            var psi = new ProcessStartInfo
            {
                FileName               = _binaryManager.BinaryPath,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = outputDir
            };

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

            var outputPath = Path.Combine(outputDir, $"{outputStem}.srt");
            if (!File.Exists(outputPath))
            {
                _logger.LogError(
                    "Subtitle file not created: {Path}\nExit code: {Code}\nstderr: {Err}\nstdout: {Out}",
                    outputPath,
                    process.ExitCode,
                    stderr.Length > 0 ? stderr.ToString() : "(empty)",
                    stdout.Length > 0 ? stdout.ToString() : "(empty)");
                return false;
            }

            return true;
        }

        private async Task<int> GetWavDurationMsAsync(string wavPath, CancellationToken ct)
        {
            var ffprobe = (_binaryManager.JellyfinFFmpegPath ?? "ffmpeg")
                .Replace("ffmpeg", "ffprobe");
            var psi = new ProcessStartInfo
            {
                FileName               = ffprobe,
                Arguments              = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{wavPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return 0;

            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
                return (int)(seconds * 1000);
            return 0;
        }

        private async Task<List<string>> SplitWavAsync(string wavPath, int chunkMs, CancellationToken ct)
        {
            var ffmpeg = _binaryManager.JellyfinFFmpegPath ?? "ffmpeg";
            var chunkSec = chunkMs / 1000.0;
            var dir = Path.GetDirectoryName(wavPath) ?? Path.GetTempPath();
            var prefix = $"whisper_chunk_{Guid.NewGuid():N}_";
            var pattern = Path.Combine(dir, $"{prefix}%03d.wav");

            _logger.LogInformation("Splitting audio ({Chunk}s chunks): {Wav} → {Pattern}",
                chunkSec, wavPath, pattern);

            var psi = new ProcessStartInfo
            {
                FileName               = ffmpeg,
                Arguments              = $"-i \"{wavPath}\" -f segment -segment_time {chunkSec} -c:a pcm_s16le -ar 16000 -ac 1 \"{pattern}\" -y -loglevel error",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                _logger.LogError("Failed to start FFmpeg for audio splitting");
                return new List<string> { wavPath };
            }

            var err = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                _logger.LogError("FFmpeg split failed ({Code}): {Err}", proc.ExitCode, err);
                return new List<string> { wavPath };
            }

            var files = Directory.GetFiles(dir, $"{prefix}*.wav")
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                _logger.LogWarning("FFmpeg split produced no files, using original");
                return new List<string> { wavPath };
            }

            _logger.LogInformation("Audio split into {Count} chunk(s)", files.Count);
            return files;
        }

        private static int MergeSrtInto(string chunkSrtPath, StringBuilder merged, int offset)
        {
            var lines = File.ReadAllLines(chunkSrtPath);
            bool expectNumber = true; // start of file → expect segment number
            int localMax = 0;

            foreach (var line in lines)
            {
                if (expectNumber && line.Length > 0 && int.TryParse(line, out var n))
                {
                    merged.AppendLine((n + offset).ToString());
                    if (n > localMax) localMax = n;
                    expectNumber = false;
                }
                else
                {
                    merged.AppendLine(line);
                    if (line.Length == 0)
                        expectNumber = true; // blank line → next line is segment number
                }
            }

            if (lines.Length > 0 && lines[^1] != "")
                merged.AppendLine();

            return offset + localMax;
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

            // Output — SRT format, stem only (no extension)
            // Output file is created in the process WorkingDirectory (set to outputDir)
            sb.Append("-osrt ");
            sb.Append($"-of \"{outputStem}\" ");

            if (translate)
                sb.Append("-tr ");

            if (wordTimestamps)
                sb.Append("-ml 1 ");

            // Thread cap
            var threads = Math.Min(Environment.ProcessorCount, 16);
            sb.Append($"-t {threads} ");

            // GPU offload (whisper-cli only supports -dev N, not -ngl)
            if (gpuType is not null)
            {
                sb.Append("-dev 0 ");
                _logger.LogInformation("{GPU} acceleration enabled", gpuType);
            }
            else
            {
                // whisper-cli defaults to use_gpu=true; explicitly disable it
                sb.Append("-ng ");
                _logger.LogInformation("CPU-only processing");
            }

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

        private async Task<bool> ExtractAudioAsync(
            string videoPath, string wavPath, CancellationToken cancellationToken)
        {
            var ffmpeg = _binaryManager.JellyfinFFmpegPath ?? "ffmpeg";

            _logger.LogInformation("Extracting audio: {Video} → {Wav}", videoPath, wavPath);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = ffmpeg,
                    Arguments              = $"-i \"{videoPath}\" -ar 16000 -ac 1 -c:a pcm_s16le -f wav \"{wavPath}\" -y -loglevel error",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    _logger.LogError("Failed to start FFmpeg process");
                    return false;
                }

                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    _logger.LogError("FFmpeg exited {Code}: {Err}", process.ExitCode, stderr);
                    return false;
                }

                if (!File.Exists(wavPath))
                {
                    _logger.LogError("FFmpeg did not produce output: {Wav}", wavPath);
                    return false;
                }

                _logger.LogInformation("Audio extracted: {Wav} ({Bytes} bytes)",
                    wavPath, new FileInfo(wavPath).Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during audio extraction from {Video}", videoPath);
                return false;
            }
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