# Architecture

## Overview

The plugin follows a straightforward pipeline:

```
Video File
    │
    ▼
Audio Extraction (FFmpeg → 16kHz mono WAV)
    │
    ▼
Duration Check
    ├── ≤ 30 min → Single chunk
    └── > 30 min → Split into 30-min chunks via FFmpeg segment muxer
                        │
                        ▼
              whisper.cpp (CPU or CUDA binary)
                        │
                        ▼
              SRT Files (one per chunk)
                        │
                        ▼
              Merge SRTs (renumber segments)
                        │
                        ▼
              Final SRT → saved next to video
```

## Key Components

### WhisperService

`Services/WhisperService.cs` — The core service that orchestrates subtitle generation.

**Key methods:**
- `GenerateSubtitleAsync()` — Main entry point: ensures binary + model are ready, extracts audio, chooses chunked or direct processing
- `RunWhisperCli()` — Launches the whisper.cpp process, captures output, handles errors
- `BuildArguments()` — Constructs command-line arguments for whisper-cli
- `GetWavDurationMsAsync()` — Uses ffprobe to measure audio duration for chunking decisions
- `SplitWavAsync()` — Uses FFmpeg segment muxer to split WAV into 30-min chunks
- `MergeSrtInto()` — Merges individual chunk SRTs into one continuous SRT

### WhisperBinaryManager

`Services/WhisperBinaryManager.cs` — Manages binary deployment and system discovery.

**Responsibilities:**
- Detecting the Jellyfin FFmpeg path
- Detecting ffprobe path
- Detecting GPU type (nvidia-smi, vulkaninfo)
- Deploying the bundled binary from plugin directory to cache
- Testing the binary with `--help`
- Finding both CPU and CUDA bundled binaries

### WhisperSubtitleTask

`Tasks/WhisperSubtitleTask.cs` — The scheduled task that iterates videos and calls WhisperService.

**Progress reporting:** Reports per-video progress with sub-progress for chunked videos using a `Progress<double>` wrapper.

### WhisperSubtitlesController

`Controllers/WhisperSubtitlesController.cs` — API endpoints used by the config page.

**Endpoints:**
- `GET /api/WhisperSubtitles/BinaryStatus` — Checks if binary is deployed
- `POST /api/WhisperSubtitles/InstallBinary` — Deploys binary from bundle to cache
- `POST /api/WhisperSubtitles/DownloadModel` — Downloads a model file
- `GET /api/WhisperSubtitles/stats` — Returns diagnostics info

## Binary Selection Logic

From `WhisperService.cs`:

```csharp
if (gpuType == "cuda" && _binaryManager.IsCudaBinaryAvailable)
{
    binaryPath = _binaryManager.CudaBinaryPath;
    // Sets LD_LIBRARY_PATH to bundled .so directory
}
else
{
    binaryPath = _binaryManager.BinaryPath; // CPU binary
}
```

## Build Arguments

The `BuildArguments` method constructs whisper-cli flags:

| Flag | Purpose | Used when |
|---|---|---|
| `-m` | Model file path | Always |
| `-f` | Input audio file | Always |
| `-l` | Language code | Always |
| `-osrt` | Output SRT format | Always |
| `-of` | Output filename stem | Always |
| `-tr` | Translate to English | When enabled |
| `-ml 1` | Max line length (word timestamps) | When enabled |
| `-t N` | Thread count | Always (min of CPU count, 16) |
| `-dev 0` | Use GPU device 0 | GPU mode |
| `-ng` | No GPU | CPU mode |

> Note: whisper-cli (the `examples/cli` target) does NOT support `--output-dir`, `-vv`, or `-ngl` flags that the `main` example supports.

## Chunking Details

- **Chunk duration:** 30 minutes (1800 seconds) — constant `ChunkDurationMs`
- **Chunking threshold:** Audio > 30 min gets chunked
- **Method:** `ffmpeg -f segment -segment_time 1800 -c:a pcm_s16le -ar 16000 -ac 1`
- **SRT merging:** Each chunk's segment numbers are offset by the previous chunk's max segment number
- **Temp file cleanup:** Chunk WAV files are deleted in a `finally` block

## GPU Detection

```csharp
private string? DetectGPU()
{
    if (CheckNvidiaGPU())  return "cuda";
    if (CheckVulkanGPU())  return "vulkan";
    if (IsOSX)             return "metal";
    return null; // CPU-only
}
```

NVIDIA detection runs `nvidia-smi --query-gpu=name --format=csv,noheader`. Vulkan detection runs `vulkaninfo --summary`.

## Cache Directory

The plugin cache follows this priority:
1. `JELLYFIN_CACHE_DIR` environment variable
2. `$HOME/.cache/` or `Path.GetTempPath()`

Structure:
```
{base}/whisper/           — Model files (ggml-*.bin)
{base}/whisper-cpp/       — Binaries (whisper-whisper-cli, whisper-whisper-cli-cuda, .so files)
```
