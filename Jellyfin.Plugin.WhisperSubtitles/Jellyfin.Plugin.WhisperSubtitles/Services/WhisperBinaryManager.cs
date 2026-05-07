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
    /// Manages the whisper.cpp binary: discovery, deployment from plugin bundle, and testing.
    /// </summary>
    public class WhisperBinaryManager : IDisposable
    {
        // The filename the build script produces inside whisper/{platform}/.
        // Must match BINARY_NAME in Build-whisper.sh.
        private const string BundledBinaryName = "whisper-whisper-cli";

        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _binaryPath;
        private readonly string _downloadPath;
        private readonly string? _jellyfinFFmpegPath;
        private string? _detectedGPUType;
        private bool _disposed;

        /// <summary>
        /// Initialises a new instance of <see cref="WhisperBinaryManager"/>.
        /// </summary>
        public WhisperBinaryManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
            })
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Jellyfin-Whisper-Plugin");

            _jellyfinFFmpegPath = FindJellyfinFFmpeg();
            _detectedGPUType    = DetectGPU();

            // ── Determine cache directory ──────────────────────────────────
            var cacheDir = Environment.GetEnvironmentVariable("JELLYFIN_CACHE_DIR");
            if (string.IsNullOrEmpty(cacheDir))
            {
                var home = Environment.GetEnvironmentVariable("HOME")
                           ?? Path.GetTempPath();
                cacheDir = Path.Combine(home, ".cache");
            }

            var whisperDir = Path.Combine(cacheDir, "whisper-cpp");
            _downloadPath  = whisperDir;
            _binaryPath    = Path.Combine(whisperDir, BundledBinaryName);

            _logger.LogInformation("=== WhisperBinaryManager Init ===");
            _logger.LogInformation("JELLYFIN_CACHE_DIR env    : {Env}", Environment.GetEnvironmentVariable("JELLYFIN_CACHE_DIR") ?? "(not set)");
            _logger.LogInformation("Resolved cache dir        : {CacheDir}", cacheDir);
            _logger.LogInformation("Whisper cache directory   : {WhisperDir}", whisperDir);
            _logger.LogInformation("Expected binary path      : {BinaryPath}", _binaryPath);
            _logger.LogInformation("Jellyfin FFmpeg           : {Path}", _jellyfinFFmpegPath ?? "not found");
            _logger.LogInformation("Detected GPU              : {GPU}",  _detectedGPUType   ?? "none (CPU only)");
            _logger.LogInformation("=====================================");

            try
            {
                Directory.CreateDirectory(whisperDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create whisper cache directory: {Dir}", whisperDir);
            }
        }

        // ── Public surface ─────────────────────────────────────────────────────

        /// <summary>Gets the path where the binary is expected to live in the cache.</summary>
        public string BinaryPath => _binaryPath;

        /// <summary>Gets the detected GPU type string, or null for CPU-only.</summary>
        public string? DetectedGPUType => _detectedGPUType;

        /// <summary>Gets Jellyfin's bundled FFmpeg path, if found.</summary>
        public string? JellyfinFFmpegPath => _jellyfinFFmpegPath;

        /// <summary>Returns true if the binary is present and executable in the cache.</summary>
        public bool IsBinaryAvailable()
        {
            // Check for any of the known binary names (supports migration across versions)
            var binaryNames = new[] { "whisper-whisper-cli", "whisper-cli", "main" };
            
            _logger.LogInformation("Checking for whisper binary in: {Path}", _downloadPath);
            
            foreach (var binaryName in binaryNames)
            {
                var candidatePath = Path.Combine(_downloadPath, binaryName);
                var exists = File.Exists(candidatePath);
                _logger.LogDebug("  Checking {BinaryName}: {Path} — {Exists}", 
                    binaryName, candidatePath, exists ? "FOUND" : "not found");
                
                if (exists)
                {
                    EnsureExecutable(candidatePath);
                    _logger.LogInformation("✓ Whisper binary found at {Path}", candidatePath);
                    return true;
                }
            }

            _logger.LogWarning("✗ Whisper binary NOT found in {Path}", _downloadPath);
            _logger.LogWarning("  Expected one of: {Names}", string.Join(", ", binaryNames));
            
            // List what files actually exist in the directory for debugging
            try
            {
                if (Directory.Exists(_downloadPath))
                {
                    var files = Directory.GetFiles(_downloadPath);
                    _logger.LogWarning("  Files in {Path}: {Files}", 
                        _downloadPath, 
                        files.Length > 0 ? string.Join(", ", files.Select(Path.GetFileName)) : "(empty)");
                }
                else
                {
                    _logger.LogWarning("  Directory does not exist: {Path}", _downloadPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list directory contents: {Path}", _downloadPath);
            }
            
            return false;
        }

        /// <summary>
        /// Deploys the bundled binary from the plugin's installation directory into the cache.
        /// This is the only "download" path — we ship the binary, we just need to copy it.
        /// Skips deployment if a binary already exists in the cache.
        /// </summary>
        public async Task<bool> DownloadBinaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Skip deployment if any binary already exists
                if (IsBinaryAvailable())
                {
                    _logger.LogInformation("Binary already available in cache, skipping deployment");
                    return true;
                }

                _logger.LogInformation("Deploying bundled whisper binary from plugin directory...");

                var source = FindBundledBinary();
                if (source is null)
                {
                    _logger.LogError(
                        "Bundled binary not found inside plugin folder. " +
                        "Expected '{Name}' inside whisper/{Platform}/ sub-directory.",
                        BundledBinaryName, GetPlatformString());
                    return false;
                }

                _logger.LogInformation("Copying {Source} → {Dest}", source, _binaryPath);
                File.Copy(source, _binaryPath, overwrite: true);
                EnsureExecutable(_binaryPath);

                return await TestBinaryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy bundled whisper binary");
                return false;
            }
        }

        /// <summary>Runs the binary with --help to verify it starts correctly.</summary>
        public async Task<bool> TestBinaryAsync(CancellationToken cancellationToken = default)
        {
            if (!IsBinaryAvailable())
                return false;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName               = _binaryPath,
                    Arguments              = "--help",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                })!;

                await process.WaitForExitAsync(cancellationToken);

                // whisper-cli --help exits with 0; some versions exit 1 but still print usage.
                // Accept both — what matters is it ran without an OS-level failure.
                var success = process.ExitCode == 0 || process.ExitCode == 1;
                _logger.LogInformation("Binary test {Result} (exit code {Code})",
                    success ? "passed" : "FAILED", process.ExitCode);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while testing whisper binary");
                return false;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Locates the bundled binary inside the plugin's own installation folder.
        /// Uses the assembly location so version-stamped folder names are handled automatically.
        /// Checks for all known binary names for backward compatibility.
        /// </summary>
        private string? FindBundledBinary()
        {
            // Assembly.Location gives us the path of THIS dll, which lives in the
            // versioned plugin folder, e.g.:
            //   /config/plugins/Whisper Subtitles_0.0.0.57/Jellyfin.Plugin.WhisperSubtitles.dll
            var assemblyDir = Path.GetDirectoryName(
                typeof(WhisperBinaryManager).Assembly.Location);

            if (string.IsNullOrEmpty(assemblyDir))
            {
                _logger.LogError("Could not determine assembly directory");
                return null;
            }

            var platform = GetPlatformString();
            var binaryNames = new[] { "whisper-whisper-cli", "whisper-cli", "main" };

            // Primary layout produced by make-release.sh / build.yaml:
            //   <plugin-dir>/whisper/<platform>/{binary-name}
            foreach (var binaryName in binaryNames)
            {
                var candidate = Path.Combine(assemblyDir, "whisper", platform, binaryName);
                _logger.LogDebug("Looking for bundled binary at: {Path}", candidate);

                if (File.Exists(candidate))
                {
                    _logger.LogInformation("Located bundled binary: {Path}", candidate);
                    return candidate;
                }
            }

            // Fallback: flat layout - check all binary names
            foreach (var binaryName in binaryNames)
            {
                var candidate = Path.Combine(assemblyDir, platform, binaryName);
                _logger.LogDebug("Fallback path: {Path}", candidate);

                if (File.Exists(candidate))
                {
                    _logger.LogInformation("Located bundled binary (fallback): {Path}", candidate);
                    return candidate;
                }
            }

            return null;
        }

        private string GetPlatformString()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64   => "linux-x64",
                    Architecture.Arm64 => "linux-arm64",
                    _                  => "linux-x64"
                };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => "macos-arm64",
                    _                  => "macos-x64"
                };
            }
            return "linux-x64"; // safe default for Docker
        }

        private static void EnsureExecutable(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                var fi = new FileInfo(filePath);
                // Set rwxr-xr-x
                fi.UnixFileMode =
                    UnixFileMode.UserRead  | UnixFileMode.UserWrite  | UnixFileMode.UserExecute  |
                    UnixFileMode.GroupRead |                            UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |                            UnixFileMode.OtherExecute;
            }
            catch
            {
                // Fallback to chmod subprocess
                try
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName  = "chmod",
                        Arguments = $"+x \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow  = true
                    });
                    p?.WaitForExit();
                }
                catch { /* best-effort */ }
            }
        }

        // ── GPU detection ──────────────────────────────────────────────────────

        private string? DetectGPU()
        {
            if (CheckNvidiaGPU())  return "cuda";
            if (CheckVulkanGPU())  return "vulkan";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "metal";
            return null;
        }

        private bool CheckNvidiaGPU()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = "nvidia-smi",
                    Arguments              = "--query-gpu=name --format=csv,noheader",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                });
                if (p is null) return false;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogInformation("NVIDIA GPU detected: {GPU}", output.Trim());
                    return true;
                }
            }
            catch { /* nvidia-smi not present */ }
            return false;
        }

        private bool CheckVulkanGPU()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = "vulkaninfo",
                    Arguments              = "--summary",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                });
                if (p is null) return false;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0 && output.Contains("deviceName"))
                {
                    _logger.LogInformation("Vulkan GPU detected");
                    return true;
                }
            }
            catch { /* vulkaninfo not present */ }
            return false;
        }

        // ── FFmpeg discovery ───────────────────────────────────────────────────

        private string? FindJellyfinFFmpeg()
        {
            var candidates = new[]
            {
                "/usr/lib/jellyfin-ffmpeg/ffmpeg",
                "/usr/lib/jellyfin-ffmpeg5/ffmpeg",
                "/usr/lib/jellyfin-ffmpeg6/ffmpeg",
                "/jellyfin/ffmpeg",
                "/config/ffmpeg/ffmpeg",
                "ffmpeg"
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        _logger.LogInformation("Found Jellyfin FFmpeg: {Path}", path);
                        return path;
                    }
                }
                catch { /* permission errors on some paths */ }
            }

            // Try `which ffmpeg`
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = "which",
                    Arguments              = "ffmpeg",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                });
                if (p is not null)
                {
                    var result = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(result) && File.Exists(result))
                    {
                        _logger.LogInformation("Found FFmpeg via which: {Path}", result);
                        return result;
                    }
                }
            }
            catch { /* which not available */ }

            _logger.LogWarning("FFmpeg not found; whisper.cpp will use built-in audio handling");
            return null;
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
            if (disposing) _httpClient?.Dispose();
            _disposed = true;
        }

        ~WhisperBinaryManager() => Dispose(false);
    }
}