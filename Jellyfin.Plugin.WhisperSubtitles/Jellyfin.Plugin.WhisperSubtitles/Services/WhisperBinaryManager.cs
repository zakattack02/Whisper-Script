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
    public class WhisperBinaryManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _binaryPath;
        private readonly string _downloadPath;
        private readonly string? _jellyfinFFmpegPath;
        private readonly string? _pluginPath;
        private string? _detectedGPUType;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperBinaryManager"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="pluginPath">Optional path to the plugin directory containing bundled binary.</param>
        public WhisperBinaryManager(ILogger logger, string? pluginPath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pluginPath = pluginPath;
            _httpClient = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip })
            {
                Timeout = TimeSpan.FromMinutes(30) // 30-minute timeout for large downloads
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Jellyfin-Whisper-Plugin");
            
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
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "whisper-cli.exe" 
                : "whisper-cli";
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
/// Extracts the bundled binary from the dynamic plugin directory to the cache.
/// </summary>
public async Task<bool> DownloadBinaryAsync(CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogInformation("Starting manual binary deployment from plugin directory...");

        var bundledBinaryPath = FindBundledBinary();
        
        if (string.IsNullOrEmpty(bundledBinaryPath))
        {
            // If we can't find it bundled, we log a very specific error helping the user
            _logger.LogError("Engine binary not found in plugin subfolders. Expected to find it in the 'whisper/{Platform}' directory of the plugin installation.", GetPlatformString());
            return false;
        }

        // Ensure target directory exists
        var binDir = Path.GetDirectoryName(_binaryPath);
        if (!Directory.Exists(binDir))
        {
            Directory.CreateDirectory(binDir!);
        }

        // Copy and Rename: 'main' (from zip) -> 'whisper-cli' (expected by service)
        _logger.LogInformation("Deploying engine: Copying {Source} to {Destination}", bundledBinaryPath, _binaryPath);
        File.Copy(bundledBinaryPath, _binaryPath, overwrite: true);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MakeExecutable(_binaryPath);
        }

        return await TestBinaryAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to deploy bundled whisper binary");
        return false;
    }
}

private string? FindBundledBinary()
{
    // 1. Get the directory where THIS DLL is actually sitting
    // This handles the "Whisper Subtitles_0.0.0.53" versioning automatically
    var assemblyLocation = typeof(WhisperBinaryManager).Assembly.Location;
    var currentPluginFolder = Path.GetDirectoryName(assemblyLocation);

    if (string.IsNullOrEmpty(currentPluginFolder)) return null;

    // 2. Determine platform (linux-x64, linux-arm64, etc.)
    var platform = GetPlatformString();
    
    // 3. Construct the search paths
    // Search both the local 'whisper' folder and the one in the zip structure
    var possibleFolders = new[]
    {
        Path.Combine(currentPluginFolder, "whisper", platform),
        Path.Combine(currentPluginFolder, platform) // fallback
    };

    var binaryNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? new[] { "whisper-cli.exe", "main.exe" } 
        : new[] { "whisper-cli", "main" };

    foreach (var folder in possibleFolders)
    {
        if (!Directory.Exists(folder)) continue;

        foreach (var name in binaryNames)
        {
            var fullPath = Path.Combine(folder, name);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Located bundled binary: {Path}", fullPath);
                return fullPath;
            }
        }
    }

    return null;
}

        /// <summary>
        /// Stub: Build from source no longer supported.
        /// </summary>
        private Task<bool> TryBuildFromSourceAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// Stub: Build script execution no longer supported.
        /// </summary>
        private Task<bool> RunBuildScriptAsync(string scriptPath, bool useSudo, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
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

                    // Look for the executable in the archive (e.g., whisper-cli or main)
                    if (entry.FullName.Contains("whisper-cli") && 
                        (entry.FullName.EndsWith("whisper-cli") || entry.FullName.EndsWith("whisper-cli.exe")))
                    {
                        var destinationPath = Path.Combine(extractPath, Path.GetFileName(entry.FullName));
                        
                        _logger.LogInformation("Extracting {Entry} to {Destination}", entry.FullName, destinationPath);
                        
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }
                        
                        entry.ExtractToFile(destinationPath, true);
                    }    
                    else if (entry.FullName.Contains("main") && 
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

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~WhisperBinaryManager()
        {
            Dispose(false);
        }
    }
}