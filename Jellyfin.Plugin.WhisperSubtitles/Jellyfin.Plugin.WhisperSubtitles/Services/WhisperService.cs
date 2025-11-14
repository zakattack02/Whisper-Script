using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Service for generating subtitles using Whisper.NET.
    public class WhisperService : IWhisperService
    {
        private readonly ILogger<WhisperService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _modelPath;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperService"/> class.
        /// <param name="logger">Logger instance.</param>
        public WhisperService(ILogger<WhisperService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            
            // Use Whisper model cache directory
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _modelPath = Path.Combine(homeDir, ".cache", "whisper");
            
            // Ensure model directory exists
            if (!Directory.Exists(_modelPath))
            {
                Directory.CreateDirectory(_modelPath);
                _logger.LogInformation("Created Whisper model directory: {ModelPath}", _modelPath);
            }
        }

        
        public async Task<bool> GenerateSubtitleAsync(
            string videoPath,
            string outputPath,
            string model,
            string language,
            bool translate,
            bool wordTimestamps,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(videoPath))
                {
                    _logger.LogError("Video file not found: {VideoPath}", videoPath);
                    return false;
                }

                _logger.LogInformation(
                    "Generating subtitles for {VideoPath} using model {Model}, language {Language}, translate: {Translate}",
                    videoPath, model, language, translate);

                // Download model
                if (!IsModelAvailable(model))
                {
                    _logger.LogInformation("Model {Model} not found, downloading...", model);
                    var downloaded = await DownloadModelAsync(model, cancellationToken);
                    if (!downloaded)
                    {
                        _logger.LogError("Failed to download model {Model}", model);
                        return false;
                    }
                }

                var modelFilePath = GetModelPath(model);
                
                // Initialize Whisper
                using var whisperFactory = WhisperFactory.FromPath(modelFilePath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage(language)
                    .Build();

                // Process the file
                await using var fileStream = File.OpenRead(videoPath);
                
                var segments = new System.Collections.Generic.List<SegmentData>();
                
                await foreach (var segment in processor.ProcessAsync(fileStream, cancellationToken))
                {
                    segments.Add(segment);
                    
                    _logger.LogDebug(
                        "Segment {Start} -> {End}: {Text}",
                        segment.Start, segment.End, segment.Text);
                }

                // Write SRT
                await WriteSrtFileAsync(outputPath, segments, cancellationToken);
                
                _logger.LogInformation(
                    "Successfully generated subtitles: {OutputPath} ({Count} segments)",
                    outputPath, segments.Count);

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

        
        public bool IsModelAvailable(string modelName)
        {
            var modelPath = GetModelPath(modelName);
            var exists = File.Exists(modelPath);
            
            _logger.LogDebug("Model {ModelName} availability: {Exists} at {Path}", 
                modelName, exists, modelPath);
            
            return exists;
        }

        
        public async Task<bool> DownloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            try
            {
                var modelPath = GetModelPath(modelName);
                
                if (File.Exists(modelPath))
                {
                    _logger.LogInformation("Model {ModelName} already exists at {Path}", modelName, modelPath);
                    return true;
                }

                _logger.LogInformation("Downloading Whisper model: {ModelName}", modelName);
                
                // Map model name to GgmlType
                var ggmlType = modelName.ToLowerInvariant() switch
                {
                    "tiny" => GgmlType.Tiny,
                    "tiny.en" => GgmlType.TinyEn,
                    "base" => GgmlType.Base,
                    "base.en" => GgmlType.BaseEn,
                    "small" => GgmlType.Small,
                    "small.en" => GgmlType.SmallEn,
                    "medium" => GgmlType.Medium,
                    "medium.en" => GgmlType.MediumEn,
                    "large" => GgmlType.LargeV3,
                    "large-v1" => GgmlType.LargeV1,
                    "large-v2" => GgmlType.LargeV2,
                    "large-v3" => GgmlType.LargeV3,
                    "turbo" => GgmlType.LargeV3Turbo,
                    _ => GgmlType.Small // Default to small
                };

                // Download the model
                var downloader = new WhisperGgmlDownloader(_httpClient);
                await using var modelStream = await downloader.GetGgmlModelAsync(ggmlType, cancellationToken: cancellationToken);
                
                // Save to file
                await using var fileStream = File.Create(modelPath);
                await modelStream.CopyToAsync(fileStream, cancellationToken);
                
                _logger.LogInformation("Successfully downloaded model {ModelName} to {Path}", modelName, modelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download model {ModelName}", modelName);
                return false;
            }
        }

        /// <summary>
        /// Get the full path for a model file.
        /// <param name="modelName">Name of the model.</param>
        /// <returns>Full path to the model file.</returns>
        private string GetModelPath(string modelName)
        {
            // Normalize model name
            var normalizedName = modelName.ToLowerInvariant();
            
            // Map to expected filename format
            var fileName = normalizedName switch
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
                _ => $"ggml-{normalizedName}.bin"
            };

            return Path.Combine(_modelPath, fileName);
        }

        /// <summary>
        /// Write segments to an SRT subtitle file.
        /// <param name="outputPath">Path to output file.</param>
        /// <param name="segments">Subtitle segments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task WriteSrtFileAsync(
            string outputPath,
            System.Collections.Generic.List<SegmentData> segments,
            CancellationToken cancellationToken)
        {
            await using var writer = new StreamWriter(outputPath);
            
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                
                // Write segment number
                await writer.WriteLineAsync($"{i + 1}");
                
                // Write timestamps in SRT format (HH:MM:SS,mmm --> HH:MM:SS,mmm)
                var startTime = FormatSrtTimestamp(segment.Start);
                var endTime = FormatSrtTimestamp(segment.End);
                await writer.WriteLineAsync($"{startTime} --> {endTime}");
                
                // Write text
                await writer.WriteLineAsync(segment.Text.Trim());
                
                // Write blank line between segments
                await writer.WriteLineAsync();
            }
        }

        /// <summary>
        /// Format a TimeSpan as SRT timestamp (HH:MM:SS,mmm).
        /// <param name="time">TimeSpan to format.</param>
        /// <returns>Formatted timestamp string.</returns>
        private string FormatSrtTimestamp(TimeSpan time)
        {
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// <param name="disposing">Whether to dispose managed resources.</param>
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
