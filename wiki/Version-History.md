# Version History

## v0.0.0.98 — CUDA GPU Support

- Multi-stage Dockerfile builds both CPU and CUDA binaries
- Plugin auto-selects CUDA binary when GPU is enabled and available
- Bundles `libcudart.so.12`, `libcublas.so.12`, `libcublasLt.so.12`
- Config page shows CUDA binary deployment status
- CUDA binary compiled for 7 GPU architectures (Maxwell through Ada)
- Package size increased to ~742 MB (was 3.5 MB with CPU-only)

## v0.0.0.97 — Per-Chunk Progress Reporting

- Scheduled task now reports progress per-chunk instead of per-video
- `IProgress<double>` passed through to chunking loop
- Smooth progress updates for long videos

## v0.0.0.96 — ffprobe Path Fix

- Added `FfprobePath` config field for manual override
- Added `FindFfprobe()` with proper fallback chain (same directory as ffmpeg)
- Fixed `.Replace("ffmpeg", "ffprobe")` bug producing wrong path

## v0.0.0.95 — OOM Prevention

- Added audio chunking for files > 30 minutes
- FFmpeg segment muxer splits WAV into 30-min chunks
- SRT merging with segment renumbering
- Temp file cleanup in `finally` block

## v0.0.0.94 — GPU Flag Fix

- Changed `-ngl 999` to `-dev 0` for GPU mode (whisper-cli doesn't support `-ngl`)

## v0.0.0.93 — AVX-512 Fix

- Added `-DGGML_NATIVE=OFF` to Dockerfile and Build-whisper.sh
- Explicit `-march=x86-64 -mtune=generic` flags
- Prevents SIGILL on CPUs without AVX-512

## v0.0.0.92 — BuildArguments Fix

- Removed `--output-dir` flag (not in whisper-cli)
- Removed `-vv` flag (not in whisper-cli)
- Changed GPU flag to `-dev 0`
- Added `-ng` for CPU mode

## Earlier Versions (v0.0.0.47 — v0.0.0.91)

Initial development versions. Core functionality was established but suffered from the issues documented in [Troubleshooting](Troubleshooting).
