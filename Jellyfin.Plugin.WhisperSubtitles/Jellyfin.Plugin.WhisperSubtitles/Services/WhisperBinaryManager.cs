using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles.Services
{
    /// <summary>
    /// Service for managing whisper.cpp binary installation.
    /// </summary>
    public class WhisperBinaryManager
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _binaryPath;
        private readonly string _downloadPath;
        private readonly string? _jellyfinFFmpegPath;
        private string? _detectedGPUType;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperBinaryManager"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public WhisperBinaryManager(ILogger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            
            // Find Jellyfin's FFmpeg
            _jellyfinFFmpegPath = FindJellyfinFFmpeg();
            
            // Determine binary storage location
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
            
            var whisperDir = Path.Combine(cacheDir, "whisper-cpp");
            _downloadPath = whisperDir;
            
            // Binary name depends on platform
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "main.exe" : "main";
            _binaryPath = Path.Combine(whisperDir, binaryName);
            
            _logger.LogInformation("Whisper binary path: {BinaryPath}", _binaryPath);
            _logger.LogInformation("Jellyfin FFmpeg path: {FFmpegPath}", _jellyfinFFmpegPath ?? "Not found");
            
            // Detect available GPU
            _detectedGPUType = DetectGPU();
            _logger.LogInformation("Detected GPU type: {GPUType}", _detectedGPUType ?? "None (CPU only)");
            
            try
            {
                if (!Directory.Exists(whisperDir))
                {
                    Directory.CreateDirectory(whisperDir);
                    _logger.LogInformation("Created whisper binary directory: {WhisperDir}", whisperDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create whisper binary directory");
            }
        }

        /// <summary>
        /// Find Jellyfin's FFmpeg installation.
        /// </summary>
        private string? FindJellyfinFFmpeg()
        {
            var possiblePaths = new[]
            {
                "/usr/lib/jellyfin-ffmpeg/ffmpeg",           // Jellyfin's bundled FFmpeg
                "/usr/lib/jellyfin-ffmpeg5/ffmpeg",          // Alternative path
                "/usr/lib/jellyfin-ffmpeg6/ffmpeg",          // FFmpeg 6.x
                "/jellyfin/ffmpeg",                          // Docker mount point
                "/config/ffmpeg/ffmpeg",                     // Custom location
                "ffmpeg"                                     // System PATH
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        _logger.LogInformation("Found Jellyfin FFmpeg at: {Path}", path);
                        return path;
                    }
                }
                catch
                {
                    // Ignore access errors
                }
            }

            // Try which command
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "ffmpeg",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var result = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    if (!string.IsNullOrEmpty(result) && File.Exists(result))
                    {
                        _logger.LogInformation("Found FFmpeg via which: {Path}", result);
                        return result;
                    }
                }
            }
            catch
            {
                // Ignore
            }

            _logger.LogWarning("Jellyfin FFmpeg not found, whisper.cpp will use built-in audio handling");
            return null;
        }

        /// <summary>
        /// Detect available GPU type (CUDA, Vulkan, Metal, or None).
        /// </summary>
        private string? DetectGPU()
        {
            // Check for NVIDIA GPU (CUDA)
            if (CheckNvidiaGPU())
            {
                return "cuda";
            }

            // Check for Vulkan support (AMD/Intel)
            if (CheckVulkanGPU())
            {
                return "vulkan";
            }

            // Check for Metal (macOS)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "metal";
            }

            return null;
        }

        /// <summary>
        /// Check if NVIDIA GPU with CUDA is available.
        /// </summary>
        private bool CheckNvidiaGPU()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name --format=csv,noheader",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        _logger.LogInformation("NVIDIA GPU detected: {GPU}", output.Trim());
                        return true;
                    }
                }
            }
            catch
            {
                // nvidia-smi not available
            }

            return false;
        }

        /// <summary>
        /// Check if Vulkan GPU is available.
        /// </summary>
        private bool CheckVulkanGPU()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "vulkaninfo",
                    Arguments = "--summary",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && output.Contains("deviceName"))
                    {
                        _logger.LogInformation("Vulkan GPU detected");
                        return true;
                    }
                }
            }
            catch
            {
                // vulkaninfo not available
            }

            return false;
        }

        /// <summary>
        /// Gets the detected GPU type.
        /// </summary>
        public string? DetectedGPUType => _detectedGPUType;

        /// <summary>
        /// Gets the path to Jellyfin's FFmpeg.
        /// </summary>
        public string? JellyfinFFmpegPath => _jellyfinFFmpegPath;

        /// <summary>
        /// Get the path to the whisper binary.
        /// </summary>
        public string BinaryPath => _binaryPath;

        /// <summary>
        /// Check if whisper.cpp binary is available.
        /// </summary>
        public bool IsBinaryAvailable()
        {
            if (!File.Exists(_binaryPath))
            {
                _logger.LogDebug("Whisper binary not found at {Path}", _binaryPath);
                return false;
            }

            // Check if file is executable (Unix)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var fileInfo = new FileInfo(_binaryPath);
                    var unixFileMode = (int)fileInfo.UnixFileMode;
                    var isExecutable = (unixFileMode & 0x49) != 0; // Check if any execute bit is set
                    
                    if (!isExecutable)
                    {
                        _logger.LogWarning("Whisper binary exists but is not executable");
                        MakeExecutable(_binaryPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check executable permissions");
                }
            }

            _logger.LogInformation("Whisper binary found at {Path}", _binaryPath);
            return true;
        }

        /// <summary>
        /// Download and install whisper.cpp binary.
        /// Strategy:
        /// 1. Try to build from source with GPU support (preferred)
        /// 2. Fallback to precompiled binary if available for platform
        /// 3. Provide user guidance if all methods fail
        /// </summary>
        public async Task<bool> DownloadBinaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting whisper.cpp installation");

                var platform = GetPlatformString();
                _logger.LogInformation("Detected platform: {Platform}", platform);

                // Try building from source with GPU support (preferred)
                if (await TryBuildFromSourceAsync(cancellationToken))
                {
                    _logger.LogInformation("Whisper.cpp built successfully with GPU support");
                    return true;
                }

                _logger.LogWarning("Build from source failed, attempting alternative installation methods");

                // Fallback: Download precompiled binary if available for this platform
                var downloadUrl = GetDownloadUrl(platform);

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.LogInformation("Downloading precompiled binary from: {Url}", downloadUrl);
                    
                    try
                    {
                        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            var totalBytes = response.Content.Headers.ContentLength ?? 0;
                            _logger.LogInformation("Download size: {Size} MB", totalBytes / 1024 / 1024);

                            var tempFile = Path.Combine(_downloadPath, "whisper-temp.download");
                            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                            {
                                await contentStream.CopyToAsync(fileStream, cancellationToken);
                            }

                            _logger.LogInformation("Download complete, extracting...");

                            if (downloadUrl.EndsWith(".zip"))
                            {
                                await ExtractZipAsync(tempFile, _downloadPath, cancellationToken);
                                File.Delete(tempFile);
                            }
                            else
                            {
                                if (File.Exists(_binaryPath))
                                {
                                    File.Delete(_binaryPath);
                                }
                                File.Move(tempFile, _binaryPath);
                            }

                            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                MakeExecutable(_binaryPath);
                            }

                            _logger.LogInformation("Whisper binary installed successfully at {Path}", _binaryPath);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to download binary: HTTP {StatusCode}", response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download precompiled binary");
                    }
                }
                else
                {
                    _logger.LogInformation("No precompiled binary available for {Platform}", platform);
                }

                // All automated methods failed - provide user guidance
                _logger.LogError("Failed to automatically install whisper.cpp");
                _logger.LogInformation("=== MANUAL INSTALLATION REQUIRED ===");
                _logger.LogInformation("Please install whisper.cpp manually:");
                _logger.LogInformation("  Linux: git clone https://github.com/ggerganov/whisper.cpp && cd whisper.cpp && make");
                _logger.LogInformation("  macOS: git clone https://github.com/ggerganov/whisper.cpp && cd whisper.cpp && make");
                _logger.LogInformation("  Then: sudo cp main /usr/local/bin/whisper");
                _logger.LogInformation("  Or set WHISPER_CPP_MAIN environment variable to the binary path");

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during whisper.cpp installation");
                return false;
            }
        }

        /// <summary>
        /// Try to build whisper.cpp from source with GPU support.
        /// Automatically handles permission issues and provides fallback methods.
        /// </summary>
        private async Task<bool> TryBuildFromSourceAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Attempting to build whisper.cpp from source...");

                // Download build script
                var scriptUrl = "https://raw.githubusercontent.com/zakattack02/Whisper-Script/refs/heads/feature/jellyfin-plugin/Jellyfin.Plugin.WhisperSubtitles/Scripts/Build-whisper.sh";
                var scriptPath = Path.Combine(_downloadPath, "build-whisper.sh");

                _logger.LogInformation("Downloading build script from: {Url}", scriptUrl);
                
                using var response = await _httpClient.GetAsync(scriptUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download build script: HTTP {Status}", response.StatusCode);
                    return false;
                }

                var scriptContent = await response.Content.ReadAsStringAsync(cancellationToken);
                await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken);

                // Make script executable
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    MakeExecutable(scriptPath);
                }

                // Run build script with non-root fallback
                _logger.LogInformation("Running build script...");
                
                // First, try running without sudo (should work if already root or in container)
                if (await RunBuildScriptAsync(scriptPath, false, cancellationToken))
                {
                    return true;
                }

                _logger.LogWarning("Build script failed, this may be due to permission restrictions");
                _logger.LogInformation("Attempting build with reduced dependency requirements...");
                
                // Try again without sudo (the script should handle its own permission issues)
                if (await RunBuildScriptAsync(scriptPath, false, cancellationToken))
                {
                    return true;
                }

                _logger.LogWarning("Build failed. User may need to install whisper.cpp manually.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build from source, will try precompiled binary");
                return false;
            }
        }

        /// <summary>
        /// Run the build script and capture output.
        /// </summary>
        private async Task<bool> RunBuildScriptAsync(string scriptPath, bool useSudo, CancellationToken cancellationToken)
        {
            try
            {
                var args = useSudo 
                    ? $"bash \"{scriptPath}\" \"{Path.GetDirectoryName(_binaryPath)}\" \"{_downloadPath}\"" 
                    : $"\"{scriptPath}\" \"{Path.GetDirectoryName(_binaryPath)}\" \"{_downloadPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = useSudo ? "sudo" : (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bash" : "/bin/bash"),
                    Arguments = useSudo ? args : $"-c 'bash {args}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start build script");
                    return false;
                }

                var output = new StringBuilder();
                var errors = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        _logger.LogInformation("Build: {Output}", e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errors.AppendLine(e.Data);
                        
                        // Permission errors are expected in restricted environments
                        if (e.Data.Contains("Permission denied") || e.Data.Contains("E: List directory"))
                        {
                            _logger.LogWarning("Build: {Error}", e.Data);
                        }
                        else
                        {
                            _logger.LogWarning("Build: {Error}", e.Data);
                        }
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0 && File.Exists(_binaryPath))
                {
                    _logger.LogInformation("Build completed successfully");
                    
                    // Clean up script
                    try { File.Delete(scriptPath); } catch { /* Ignore */ }
                    
                    return true;
                }
                else
                {
                    if (process.ExitCode != 0)
                    {
                        _logger.LogWarning("Build script failed with exit code {Code}", process.ExitCode);
                    }
                    
                    if (!string.IsNullOrEmpty(errors.ToString()))
                    {
                        _logger.LogWarning("Build stderr: {Errors}", errors.ToString().Substring(0, Math.Min(500, errors.Length)));
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Build script execution failed");
                return false;
            }
        }

        /// <summary>
        /// Get platform string for download URL.
        /// </summary>
        private string GetPlatformString()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return "linux-x64";
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    return "linux-arm64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return "windows-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return "macos-x64";
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    return "macos-arm64";
            }

            return "unknown";
        }

        /// <summary>
        /// Get download URL for whisper.cpp binary.
        /// Supports official whisper.cpp releases and community-provided precompiled binaries.
        /// </summary>
        private string? GetDownloadUrl(string platform)
        {
            // Try multiple sources for precompiled binaries
            // Primary: Official whisper.cpp releases (Windows only)
            // Fallback: Community-provided binaries via direct URLs
            // Last resort: Build from source (returns null to trigger build)

            if (platform == "windows-x64")
            {
                // Windows: Official whisper.cpp releases
                var version = "v1.8.2";
                var baseUrl = $"https://github.com/ggml-org/whisper.cpp/releases/download/{version}";
                return $"{baseUrl}/whisper-bin-x64.zip";
            }
            else if (platform == "linux-x64" || platform == "linux-arm64")
            {
                // Linux: Provide direct link to community-built binaries if available
                // For now, return null to trigger build from source (preferred for GPU support)
                // Users can also install pre-built packages or build manually
                _logger.LogInformation("No precompiled binary available for {Platform}. Will attempt to build from source or recommend manual installation.", platform);
                return null;
            }
            else if (platform == "macos-x64" || platform == "macos-arm64")
            {
                // macOS: Also requires building from source for best compatibility
                return null;
            }

            _logger.LogWarning("Unknown platform: {Platform}", platform);
            return null;
        }

        /// <summary>
        /// Extract zip archive.
        /// </summary>
        private async Task ExtractZipAsync(string zipPath, string extractPath, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Look for the main executable
                    if (entry.FullName.Contains("main") && 
                        (entry.FullName.EndsWith("main") || entry.FullName.EndsWith("main.exe")))
                    {
                        var destinationPath = Path.Combine(extractPath, Path.GetFileName(entry.FullName));
                        
                        _logger.LogInformation("Extracting {Entry} to {Destination}", entry.FullName, destinationPath);
                        
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }
                        
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Make file executable on Unix systems.
        /// </summary>
        private void MakeExecutable(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                // Use chmod command to make executable
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Made {Path} executable", filePath);
                }
                else
                {
                    _logger.LogWarning("chmod failed with exit code {Code}", process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to make file executable");
                
                // Fallback: try using FileInfo.UnixFileMode (requires .NET 7+)
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    fileInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                    _logger.LogInformation("Set executable permissions using UnixFileMode");
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Failed to set UnixFileMode");
                }
            }
        }

        /// <summary>
        /// Test if the binary works.
        /// </summary>
        public async Task<bool> TestBinaryAsync(CancellationToken cancellationToken = default)
        {
            if (!IsBinaryAvailable())
                return false;

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _binaryPath,
                        Arguments = "--help",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync(cancellationToken);

                var success = process.ExitCode == 0;
                _logger.LogInformation("Binary test {Result}", success ? "passed" : "failed");
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test whisper binary");
                return false;
            }
        }
    }
}