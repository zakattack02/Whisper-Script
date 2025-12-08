using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperBinaryManager"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public WhisperBinaryManager(ILogger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            
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
        /// </summary>
        public async Task<bool> DownloadBinaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting whisper.cpp binary download");

                // Determine platform and architecture
                var platform = GetPlatformString();
                var downloadUrl = GetDownloadUrl(platform);

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.LogError("Unsupported platform: {Platform}", platform);
                    return false;
                }

                _logger.LogInformation("Downloading from: {Url}", downloadUrl);

                // Download the binary/archive
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                _logger.LogInformation("Download size: {Size} MB", totalBytes / 1024 / 1024);

                // Download to temp file
                var tempFile = Path.Combine(_downloadPath, "whisper-temp.download");
                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                {
                    await contentStream.CopyToAsync(fileStream, cancellationToken);
                }

                _logger.LogInformation("Download complete, extracting...");

                // Extract if it's a zip file
                if (downloadUrl.EndsWith(".zip"))
                {
                    await ExtractZipAsync(tempFile, _downloadPath, cancellationToken);
                    File.Delete(tempFile);
                }
                else
                {
                    // Direct binary download - just move it
                    if (File.Exists(_binaryPath))
                    {
                        File.Delete(_binaryPath);
                    }
                    File.Move(tempFile, _binaryPath);
                }

                // Make executable on Unix
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    MakeExecutable(_binaryPath);
                }

                _logger.LogInformation("Whisper binary installed successfully at {Path}", _binaryPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download whisper.cpp binary");
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
        /// Uses precompiled binaries from whisper.cpp releases.
        /// </summary>
        private string GetDownloadUrl(string platform)
        {
            // Using whisper.cpp releases from GitHub
            // These are precompiled binaries that don't require compilation
            var version = "1.7.1"; // Latest stable version
            var baseUrl = $"https://github.com/ggerganov/whisper.cpp/releases/download/v{version}";

            return platform switch
            {
                "linux-x64" => $"{baseUrl}/whisper-bin-linux-x64.zip",
                "windows-x64" => $"{baseUrl}/whisper-bin-win64.zip",
                "macos-x64" => $"{baseUrl}/whisper-bin-macos-x64.zip",
                "macos-arm64" => $"{baseUrl}/whisper-bin-macos-arm64.zip",
                _ => null
            };
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
