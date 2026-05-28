# Whisper Subtitles Plugin for Jellyfin

**Version 3.0.0.0** | [GitHub](https://github.com/zakattack02/Whisper-Script) | Jellyfin 10.11.2 | .NET 9.0

Generate AI-powered subtitles for your media library using OpenAI's Whisper model via whisper.cpp. Fully local -- no external API calls, no data leaves your server. Runs on CPU or NVIDIA GPU (CUDA).

## Features

- **Fully local** -- no API keys, no subscriptions, no data sent to third parties
- **Multiple Whisper models** -- Tiny, Base, Small, Medium, Large, Turbo with English-only variants
- **Translation to English** -- transcribe any language, translate to English
- **CPU and GPU** -- runs on CPU (AVX2) or NVIDIA GPU via CUDA with automatic binary selection
- **Audio chunking** -- splits audio >30 minutes into chunks to prevent OOM on low-RAM servers (4 GB)
- **Word-level timestamps** -- precise subtitle timing (approximately 2-3x slower processing)
- **Scheduled task** -- process entire libraries on a schedule
- **Library scan hook** -- auto-generate subtitles for new media as it is discovered
- **Per-chunk progress reporting** -- smooth progress updates in the scheduled task UI
- **Intelligent subtitle detection** -- skip existing subtitles, force-regenerate AI-tagged subtitles
- **AI identifier tagging** -- mark generated subtitles with a configurable tag (e.g., `video.en.whisper.srt`)
- **Library filtering** -- select which libraries to process, exclude specific folders
- **FFmpeg audio extraction** -- works with any video format (MP4, MKV, AVI, MOV, etc.)
- **Dual binary system** -- ships both CPU (4.2 MB) and CUDA (~1.1 GB) whisper-cli binaries

## Requirements

- **Jellyfin 10.11.x** (10.11.2 is the target ABI)
- **Linux x86_64** server (binaries compiled for linux-x64)
- **~750 MB free disk space** for the combined plugin zip (CPU + CUDA)
- **Additional disk space** for model files (75 MB for Tiny, up to 3 GB for Large)
- **FFmpeg** (bundled with Jellyfin, auto-detected)
- **NVIDIA GPU** (optional, for CUDA acceleration)
- **Docker** (optional, for building from source)

## Installation

### Method 1: Repository Manifest

1. Dashboard -> Plugins -> Repositories -> **Add**
2. URL: `https://github.com/zakattack02/Whisper-Script/raw/refs/heads/feature/jellyfin-plugin/manifest.json`
3. Name: `Whisper Subtitles`
4. Save, then Catalog -> Install -> Restart Jellyfin

### Method 2: Manual ZIP

Download the latest release zip from [GitHub Releases](https://github.com/zakattack02/Whisper-Script/releases), then Dashboard -> Plugins -> **Manual Install** -> Upload the zip -> Restart.

### Method 3: Filesystem Deployment

```bash
# Extract into the Jellyfin plugins directory
sudo unzip jellyfin-plugin-whispersubtitles_3.0.0.0.zip \
    -d /var/lib/jellyfin/plugins/Whisper\ Subtitles_3.0.0.0/
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/Whisper\ Subtitles_3.0.0.0/
sudo systemctl restart jellyfin
```

### Post-Install

On first use, the plugin automatically deploys the whisper binary from the bundle to `~/.cache/whisper-cpp/` and downloads the selected model to `~/.cache/whisper/`. When upgrading, clear the old binary cache:

```bash
rm -rf /cache/whisper-cpp/   # or ~/.cache/whisper-cpp/
```

## Configuration

Navigate to Dashboard -> Plugins -> Whisper Subtitles -> Settings.

### Model & Engine

| Setting | Description | Default |
|---|---|---|
| **Whisper Model** | Model size (larger = more accurate, slower, more VRAM) | Small |
| **Download Model** | Pre-download the selected model | Button |
| **Target Language** | Language code for subtitles (e.g., en, es, fr, de, ja, zh) | `en` |
| **AI Identifier** | Tag appended to subtitle filenames (e.g., `video.en.whisper.srt`) | `whisper` |

### Acceleration

| Setting | Description | Default |
|---|---|---|
| **Enable CUDA (NVIDIA GPU)** | Use the CUDA GPU binary instead of the CPU binary | Enabled |
| **FFprobe Path** | Custom path to ffprobe (used for audio duration detection) | Auto-detect |

The config page shows runtime diagnostics: detected GPU type (cuda, vulkan, metal, or none), CUDA binary deployment status, available CPU threads, and cached model count.

### Library Automation

| Setting | Description | Default |
|---|---|---|
| **Process on Library Scan** | Auto-generate subtitles for new media on library scan | Disabled |
| **Skip Existing Subtitles** | Skip videos that already have subtitle tracks | Enabled |
| **Regenerate AI Subtitles** | Force-regenerate even if AI-tagged subtitle exists | Disabled |
| **Translate to English** | Translate non-English audio to English subtitles | Disabled |
| **Word-Level Timestamps** | More precise timing (2-3x slower processing) | Disabled |
| **Show in Main Menu** | Toggle the plugin entry in Jellyfin sidebar navigation | Enabled |
| **Libraries to Process** | Select which media libraries to scan (empty = all) | All |
| **Folders to Exclude** | Absolute paths to exclude (one per line) | Empty |

## Usage

### Scheduled Task

Dashboard -> Scheduled Tasks -> **Generate Whisper Subtitles** -> Play button to run immediately, or configure a trigger (e.g., daily at 2 AM).

### Library Scan Hook

Enable **Process on Library Scan** in settings. Subtitles are auto-generated for new media items when a library scan completes or new files are detected.

### Processing Pipeline

For each video:

1. **Skip check** -- skip if existing subtitles are found (respecting Skip Existing and Regenerate AI settings)
2. **Audio extraction** -- FFmpeg extracts 16 kHz mono WAV
3. **Duration check** -- if audio >30 minutes, split into 30-minute chunks via FFmpeg segment muxer
4. **Transcription** -- each chunk (or the full audio) processed by whisper.cpp (CPU or CUDA)
5. **SRT merging** -- chunk SRTs merged into one with sequential segment numbering
6. **File tagging** -- output saved as `video.{lang}.{identifier}.srt` next to the video

### File Naming

```
/Media/Movies/My Movie (2024).mkv
/Media/Movies/My Movie (2024).en.whisper.srt
```

### Monitoring

The scheduled task UI shows percentage progress with per-chunk updates. Check Jellyfin logs for detailed per-step logging:

```
[INF] Whisper task starting. Model=Small, Language="en", Translate=False, Identifier="whisper"
[INF] Generating: /Media/Movies/My Movie (2024).mkv
[INF] Using CUDA binary at /cache/whisper-cpp/whisper-whisper-cli-cuda
[INF] Subtitles written: My Movie (2024).en.whisper.srt (12345 bytes)
[INF] Task complete. Generated=1, Skipped=0, Errors=0
```

## Model Information

Models are downloaded from Hugging Face (`ggerganov/whisper.cpp`) and cached in `~/.cache/whisper/`.

| Model | Size | Speed (vs Large) | VRAM | Quality |
|---|---|---|---|---|
| Tiny / Tiny.en | 75 MB | ~10x | ~1 GB | Lowest |
| Base / Base.en | 140 MB | ~7x | ~1 GB | Low |
| **Small / Small.en** | **460 MB** | **~4x** | **~2 GB** | **Recommended** |
| Medium / Medium.en | 1.5 GB | ~2x | ~5 GB | High |
| Turbo | 1.6 GB | ~8x | ~6 GB | High (fast) |
| Large (v3) | 3 GB | 1x | ~10 GB | Best |

> Small is the default and recommended starting point. Turbo is nearly as accurate as Large but 8x faster.

### Memory Usage with Audio Chunking

| Model | RAM per chunk |
|---|---|
| Tiny | ~300 MB |
| Base | ~400 MB |
| Small | ~800 MB |
| Turbo | ~2 GB |
| Medium | ~2 GB |
| Large | ~3.8 GB |

## GPU Acceleration

The plugin ships two separate whisper binaries:

| Binary | Purpose | Size |
|---|---|---|
| `whisper-whisper-cli` | CPU-only (AVX2) | 4.2 MB |
| `whisper-whisper-cli-cuda` | CUDA GPU | ~1.1 GB |

Three CUDA shared libraries are bundled alongside the CUDA binary: `libcudart.so.12` (692 KB), `libcublas.so.12` (105 MB), `libcublasLt.so.12` (422 MB). The plugin sets `LD_LIBRARY_PATH` to locate them at runtime. The NVIDIA driver library `libcuda.so.1` is NOT bundled -- it comes from the host driver via container GPU passthrough.

### Prerequisites

- **NVIDIA driver** installed on the host (verify with `nvidia-smi`)
- **nvidia-container-toolkit** installed for Docker:
  ```bash
  sudo apt-get install nvidia-container-toolkit
  sudo systemctl restart docker
  ```
- Container started with `--gpus all` or `runtime: nvidia`

### Binary Selection Logic

1. User checks "Enable CUDA" in config
2. `nvidia-smi` detects NVIDIA GPU -> GPU type = "cuda"
3. CUDA binary is found in cache -> use CUDA binary with `-dev 0` flag
4. Falls back to CPU binary with `-ng` flag if any step fails

### Performance (relative to CPU on Xeon E5-2660 v3)

| Model | CPU (6 threads) | GPU (RTX 3060) | Speedup |
|---|---|---|---|
| Tiny | ~0.3x realtime | ~40x realtime | ~130x |
| Base | ~0.8x realtime | ~30x realtime | ~37x |
| Small | ~0.4x realtime | ~15x realtime | ~37x |

A 30-minute chunk on CPU takes ~23 minutes with Base model. On CUDA it takes ~1 minute.

## Troubleshooting

### SIGILL (exit code 132) -- Illegal Instruction

**Cause:** Binary compiled with AVX-512 instructions running on a CPU without AVX-512 support.

**Fix:** Rebuild with `-DGGML_NATIVE=OFF`, `-DCMAKE_C_FLAGS="-march=x86-64 -mtune=generic"`. Fixed in v1.1.1.0+.

### OOM Killer (exit code 137)

**Cause:** Long audio files loaded entirely into memory exceed available RAM.

**Fix:** Audio chunking splits files >30 minutes into 30-minute segments. Each chunk stays within ~4 GB even with the Large model. Fixed in v2.0.0.0+.

### ffprobe Not Found

**Cause:** Plugin could not locate the ffprobe binary.

**Fix:** Set the **FFprobe Path** in Settings, or ensure ffprobe is in the same directory as ffmpeg. Fixed in v2.1.0.0+.

### No GPU Found / CUDA Not Available

**Check:** Config page shows "Runtime Hardware Status" (should be "cuda"), CUDA binary is deployed, container has `--gpus all`, `docker exec jellyfin nvidia-smi` works.

### Binary Not Deployed

**Cause:** Plugin zip was extracted incorrectly or binary cache is stale.

**Fix:** Click **Deploy Runtime Binary** on the config page, or delete `~/.cache/whisper-cpp/` and reinstall.

### Plugin Config Page Shows Diagnostic Info

The config page shows binary deployment status, GPU type, cached model count, and available CPU threads. If diagnostics show "unconfigured", trigger the task once or click Deploy Runtime Binary.

## Build from Source

### Prerequisites

- Docker (recommended for GLIBC-compatible builds)
- .NET SDK 9.0
- git, cmake, build-essential
- `gh` CLI (optional, for publishing releases)

### Quick Build

```bash
# From the repo root
bash make-release.sh
```

This builds whisper.cpp in Docker (CPU + CUDA), builds the C# plugin, packages the zip, and optionally publishes to GitHub.

### Building whisper.cpp Only

```bash
# Docker build (recommended)
bash Jellyfin.Plugin.WhisperSubtitles/Scripts/Build-whisper.sh \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/bin/whisper/linux-x64/

# Native build (fallback -- may require GLIBC 2.43+)
bash Jellyfin.Plugin.WhisperSubtitles/Scripts/Build-whisper.sh --no-docker \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/bin/whisper/linux-x64/
```

### Building the C# Plugin Only

```bash
dotnet publish --configuration Release \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles.csproj
```

### Docker Build Internals

The multi-stage Dockerfile (`Scripts/Dockerfile.whisper`):

- **Stage 1 (cpu-builder):** Ubuntu 22.04, `-DGGML_CUDA=OFF` -> `whisper-whisper-cli`
- **Stage 2 (cuda-builder):** `nvidia/cuda:12.4.1-devel-ubuntu22.04`, `-DGGML_CUDA=ON`, targets GPU architectures 50/60/70/75/80/86/89 -> `whisper-whisper-cli-cuda` + libcudart/libcublas/libcublasLt
- **Stage 3 (output):** Copies all artifacts

### Key CMake Flags

| Flag | Purpose |
|---|---|
| `-DGGML_NATIVE=OFF` | Prevents AVX-512 instructions in the binary |
| `-DGGML_OPENMP=OFF` | Disables OpenMP (not needed, uses own threading) |
| `-DGGML_CUDA=ON` | Enables CUDA GPU support (CUDA stage only) |
| `-DCMAKE_CUDA_ARCHITECTURES` | Target GPU architectures (each adds ~150 MB to binary) |
| `-DCMAKE_C_FLAGS=-march=x86-64 -mtune=generic` | Maximum CPU compatibility |
| `-DCMAKE_EXE_LINKER_FLAGS=-static-libgcc -static-libstdc++` | Static GCC/GLIBCXX linkage |

## Version History

| Version | Changes |
|---|---|
| **3.0.0.0** | Semantic versioning restructure. Dual CPU+GPU binaries with CUDA support. Dockerfile builds both binaries + bundles CUDA .so files. Plugin auto-selects CUDA binary. |
| **2.2.0.0** | Per-video sub-progress reporting. Progress bar updates per chunk instead of freezing per video. |
| **2.1.0.0** | Fixed ffprobe path detection. Added FfprobePath config field + FindFfprobe() fallback chain. |
| **2.0.0.0** | Audio chunking for >30 min videos to prevent OOM on low-RAM servers (4 GB). Chunked processing + SRT merging. |
| **1.1.2.0** | Fixed `-ngl 999` -> `-dev 0` for GPU path. |
| **1.1.1.0** | Fixed AVX-512 SIGILL: added `GGML_NATIVE=OFF` to cmake. |
| **1.1.0.0** | Fixed whisper-cli arguments: removed unsupported `--output-dir` and `-vv` flags, added `-ng` for CPU mode. |
| **1.0.0.0** | Initial release. FFmpeg audio extraction, config page, Jellyfin 10.11.2 compatibility. |

## Architecture

```
Video File
    |
    v
Audio Extraction (FFmpeg -> 16 kHz mono WAV)
    |
    v
Duration Check
    +-- <=30 min -> Single chunk
    +-- >30 min  -> Split into 30-min chunks via FFmpeg segment muxer
                        |
                        v
              whisper.cpp (CPU or CUDA binary)
                        |
                        v
              Per-chunk SRT files
                        |
                        v
              Merge SRTs (renumber segments)
                        |
                        v
              Final SRT -> saved next to video
```

### Cache Directory Structure

```
~/.cache/
  whisper/             -- Model files (ggml-*.bin)
  whisper-cpp/         -- Binaries and CUDA .so files
    whisper-whisper-cli
    whisper-whisper-cli-cuda
    libcudart.so.12
    libcublas.so.12
    libcublasLt.so.12
```

## License

MIT License. This plugin uses [whisper.cpp](https://github.com/ggerganov/whisper.cpp) by Georgi Gerganov and [OpenAI Whisper](https://github.com/openai/whisper).

## Links

- [GitHub Repository](https://github.com/zakattack02/Whisper-Script)
- [Installation](wiki/Installation)
- [Configuration](wiki/Configuration)
- [Usage](wiki/Usage)
- [Architecture](wiki/Architecture)
- [GPU Acceleration](wiki/GPU-Acceleration)
- [Build from Source](wiki/Build-from-Source)
- [Troubleshooting](wiki/Troubleshooting)
- [Version History](wiki/Version-History)
- [Development](wiki/Development)
