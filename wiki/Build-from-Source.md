# Build from Source

## Prerequisites

- **Docker** (recommended) — for GLIBC-compatible builds
- **.NET SDK 9.0** — for building the C# plugin
- **git** — for version control
- **cmake, build-essential** — for native whisper.cpp builds (fallback)
- **`gh` CLI** (optional) — for publishing releases to GitHub

## Quick Build

The easiest way to build everything is using `make-release.sh`:

```bash
# From the repo root
bash make-release.sh
```

This will:
1. Build whisper.cpp in Docker (CPU + CUDA binaries)
2. Build the C# plugin
3. Package the zip
4. Optionally publish to GitHub

## Building whisper.cpp Only

### Docker Build (Recommended)

```bash
bash Scripts/Build-whisper.sh \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/bin/whisper/linux-x64/
```

This produces:
- `whisper-whisper-cli` — CPU binary
- `whisper-whisper-cli-cuda` — CUDA binary (requires nvidia/cuda Docker image)
- `libcudart.so.12` — bundled CUDA runtime
- `libcublas.so.12` — bundled CUDA BLAS
- `libcublasLt.so.12` — bundled CUDA BLAS Light

### Native Build (Fallback)

```bash
bash Scripts/Build-whisper.sh --no-docker \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/bin/whisper/linux-x64/
```

> **Warning:** Native builds may require GLIBC 2.43+ (depends on host system). Use Docker for maximum compatibility.

## Building the C# Plugin Only

```bash
dotnet build \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles.csproj
```

Or for a publish-ready build:

```bash
dotnet publish \
    --configuration Release \
    Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles.csproj
```

## Full Release Process

1. Ensure you're on the `feature/jellyfin-plugin` branch
2. Run `bash make-release.sh`
3. Select version increment (or keep current version)
4. Enter changelog
5. Confirm — the script commits, tags, pushes, and creates a GitHub release

## Docker Build Internals

The multi-stage Dockerfile (`Scripts/Dockerfile.whisper`):

```
Stage 1: cpu-builder
  Base: ubuntu:22.04
  Build: cmake -DGGML_CUDA=OFF ...
  Output: whisper-cli → whisper-whisper-cli

Stage 2: cuda-builder
  Base: nvidia/cuda:12.4.1-devel-ubuntu22.04
  Build: cmake -DGGML_CUDA=ON -DCMAKE_CUDA_ARCHITECTURES="50;60;70;75;80;86;89" ...
  Output: whisper-cli → whisper-whisper-cli-cuda
  Extract: libcudart.so.12, libcublas.so.12, libcublasLt.so.12

Stage 3: output
  Base: ubuntu:22.04 (scratch equivalent)
  Copy: all artifacts from stages 1 & 2
  CMD: copy to /output/
```

## Important CMake Flags

| Flag | Purpose |
|---|---|
| `-DGGML_NATIVE=OFF` | Prevents `-march=native` to avoid AVX-512 in the binary |
| `-DGGML_OPENMP=OFF` | Disables OpenMP (not needed, uses own threading) |
| `-DGGML_CUDA=ON` | Enables CUDA GPU support (cuda-builder stage only) |
| `-DWHISPER_BUILD_TESTS=OFF` | Faster build, no tests |
| `-DBUILD_SHARED_LIBS=OFF` | Static linking for whisper library |
| `-DCMAKE_CUDA_ARCHITECTURES` | Target GPU architectures (CUDA build only) |
| `-DCMAKE_C_FLAGS=-march=x86-64 -mtune=generic` | Maximum CPU compatibility |
| `-DCMAKE_EXE_LINKER_FLAGS=-static-libgcc -static-libstdc++` | Static GCC/GLIBCXX linkage |

## Reducing CUDA Binary Size

The CUDA binary is ~1.1 GB because GPU kernels are compiled for 7 architectures. To reduce size:

1. Edit `Dockerfile.whisper` and change `CMAKE_CUDA_ARCHITECTURES` to target only your GPU
2. Common targets:
   - `"75"` — Turing (RTX 2060, 2080, T4)
   - `"86"` — Ampere (RTX 3060, 3080, A10)
   - `"89"` — Ada (RTX 4060, 4090)
   - `"50"` — Maxwell (very old GPUs, rarely needed)
3. Each architecture adds ~150 MB to the binary
