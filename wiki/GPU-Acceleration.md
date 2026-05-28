# GPU Acceleration (CUDA)

## Overview

Starting with v0.0.0.98, the plugin ships **two separate binaries**:

| Binary | Purpose | Size |
|---|---|---|
| `whisper-whisper-cli` | CPU-only (AVX2) | 4.2 MB |
| `whisper-whisper-cli-cuda` | CUDA GPU | ~1.1 GB |

The CUDA binary is large because whisper.cpp compiles GPU kernels for 7 architectures embedded directly in the binary (Maxwell 5.0 through Ada Lovelace 8.9).

## Prerequisites

### On the Docker Host

1. **NVIDIA driver** installed and working (verify with `nvidia-smi`)
2. **nvidia-container-toolkit** installed:

```bash
# Ubuntu/Debian
sudo apt-get install nvidia-container-toolkit
sudo systemctl restart docker
```

3. **GPU** must be accessible from inside the container

### In the Jellyfin Container

The container must be started with GPU passthrough:

```bash
docker run -d \
    --name jellyfin \
    --gpus all \
    -v /path/to/cache:/cache \
    # ... other volumes
    jellyfin/jellyfin
```

Or using docker-compose:

```yaml
services:
  jellyfin:
    image: jellyfin/jellyfin
    runtime: nvidia
    environment:
      - NVIDIA_VISIBLE_DEVICES=all
    # ...
```

## How the Dual-Binary System Works

1. **Build time:** The Dockerfile builds both binaries in a multi-stage build
2. **Bundle time:** make-release.sh packages both binaries + CUDA .so files into the plugin zip
3. **Deploy time:** WhisperBinaryManager copies both to the cache directory
4. **Runtime:** WhisperService selects the binary based on config:

```
User checks "Enable CUDA" ✓
    ↓
DetectGPU() returns "cuda" (nvidia-smi works in container)
    ↓
IsCudaBinaryAvailable() == true (CUDA binary in cache)
    ↓
Use CUDA binary at /cache/whisper-cpp/whisper-whisper-cli-cuda
    ↓
Set LD_LIBRARY_PATH to bundled .so directory
    ↓
BuildArguments() → "-dev 0" (GPU device 0)
    ↓
Run CUDA binary
```

If any step fails (no GPU detected, CUDA binary not found), the plugin logs a warning and falls back to the CPU binary.

## Bundled CUDA Libraries

The plugin bundles three shared libraries from the CUDA toolkit:

| Library | Size | Purpose |
|---|---|---|
| `libcudart.so.12` | 692 KB | CUDA Runtime API |
| `libcublas.so.12` | 105 MB | CUDA BLAS (matrix operations) |
| `libcublasLt.so.12` | 422 MB | CUDA BLAS Light (optimized kernels) |

These are extracted alongside the CUDA binary. The plugin sets `LD_LIBRARY_PATH` to find them.

The NVIDIA driver library `libcuda.so.1` is NOT bundled — it comes from the host's NVIDIA driver via the container's GPU passthrough.

## Verification

### From the Config Page

Check Dashboard → Plugins → Whisper Subtitles → Settings:

- **Runtime Hardware Status:** shows "cuda" if NVIDIA GPU is detected
- **CUDA Binary:** shows "✓ deployed and ready" if the CUDA binary is available

### From Jellyfin Logs

```
[INF] Detected GPU: cuda
[INF] CUDA binary at /cache/whisper-cpp/whisper-whisper-cli-cuda
[INF] Using CUDA binary at /cache/whisper-cpp/whisper-whisper-cli-cuda
```

### From Inside the Container

```bash
# Check GPU access
docker exec jellyfin nvidia-smi

# Verify CUDA binary is deployed
docker exec jellyfin ls -la /cache/whisper-cpp/whisper-whisper-cli-cuda

# Check bundled .so files
docker exec jellyfin ls -la /cache/whisper-cpp/libcublas*.so*
```

## Performance

Typical speedups with CUDA (relative to CPU-only on a Xeon E5-2660 v3, 6 threads):

| Model | CPU (6 threads) | GPU (RTX 3060) | Speedup |
|---|---|---|---|
| Tiny | ~0.3x realtime | ~40x realtime | ~130x |
| Base | ~0.8x realtime | ~30x realtime | ~37x |
| Small | ~0.4x realtime | ~15x realtime | ~37x |
| Turbo | — | ~20x realtime | — |

> A 30-minute chunk on CPU takes ~23 minutes with Base model. On CUDA it takes ~1 minute.

## Troubleshooting GPU

| Symptom | Likely Cause | Solution |
|---|---|---|
| "no GPU found" in logs | Container doesn't have `--gpus all` | Restart with GPU passthrough |
| "CUDA binary not available" | CUDA binary not deployed | Delete cache, reinstall plugin |
| nvidia-smi works on host but not in container | nvidia-container-toolkit not installed | Install and restart Docker |
| Plugin shows GPU but whisper uses CPU | "Enable CUDA" not checked | Check the checkbox in config |
