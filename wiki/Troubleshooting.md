# Troubleshooting

## SIGILL (exit code 132) — Illegal Instruction

**Symptom:** whisper-cli crashes immediately with exit code 132 (SIGILL).

**Cause:** The binary was compiled with AVX-512 instructions (from `-DGGML_NATIVE=ON` on an AVX-512-capable CPU like Ryzen 9950X3D) but is running on a CPU that doesn't support AVX-512 (like Xeon E5-2660 v3).

**Fix:** Rebuild with `-DGGML_NATIVE=OFF`:

```bash
# In Dockerfile.whisper or cmake:
-DGGML_NATIVE=OFF
-DCMAKE_C_FLAGS="-march=x86-64 -mtune=generic"
-DCMAKE_CXX_FLAGS="-march=x86-64 -mtune=generic"
```

**Verification:** Check which SIMD extensions the binary uses:

```bash
objdump -T whisper-cli | grep -i avx
```

A correct build shows `AVX` and `AVX2` but NOT `AVX512`.

*Applies to: v0.0.0.92 and earlier. Fixed in v0.0.0.93+.*

## "Unknown argument" — exit(0) without processing

**Symptom:** whisper-cli exits immediately with code 0, no error, no processing.

**Cause:** Using flags that the `whisper-cli` target doesn't support:

| Flag | Status | Alternative |
|---|---|---|
| `--output-dir` | Not in `whisper-cli` | Use `-of` with `WorkingDirectory` |
| `-vv` | Not in `whisper-cli` | Remove (default verbosity is sufficient) |
| `-ngl N` | Not in `whisper-cli` | Use `-dev 0` for GPU, `-ng` for CPU |

*Applies to: v0.0.0.91 and earlier. Fixed in v0.0.0.92+.*

## OOM Killer (exit code 137)

**Symptom:** Process killed by OOM (exit code 137 / SIGKILL), especially with long videos on low-memory servers.

**Cause:** Whisper loads the entire audio as PCM float32 into memory. A 3-hour WAV is ~307MB, but as PCM float32 it's ~614MB, plus model weights (e.g., Base = 140MB), plus working memory → exceeds 4GB.

**Fix:** Audio chunking splits long audio into 30-minute segments. Each segment is ~60MB WAV → ~120MB PCM → well within 4GB even with a Large model:

| Model | RAM with chunking (per chunk) |
|---|---|
| Tiny | ~300 MB |
| Base | ~400 MB |
| Small | ~800 MB |
| Turbo | ~2 GB |
| Medium | ~2 GB |
| Large | ~3.8 GB |

*Applies to: v0.0.0.94 and earlier. Fixed in v0.0.0.95+.*

## ffprobe Not Found (Win32Exception / exit code 2)

**Symptom:** Error starting ffprobe process:

```
Win32Exception: ApplicationName='/usr/lib/jellyfin-ffprobe/ffprobe'
The system cannot find the file specified
```

**Cause:** The old code used `.Replace("ffmpeg", "ffprobe")` on the ffmpeg path. If ffmpeg is at `/usr/lib/jellyfin-ffmpeg/ffmpeg`, this produces `/usr/lib/jellyfin-ffprobe/ffprobe` (wrong — the directory is `jellyfin-ffmpeg`, not `jellyfin-ffprobe`).

**Fix:** v0.0.0.96+ uses `FindFfprobe()` which looks:
1. Next to the found ffmpeg binary (same directory)
2. Known paths: `/usr/lib/jellyfin-ffmpeg/ffprobe`, `/usr/lib/jellyfin-ffmpeg5/ffprobe`, etc.
3. `which ffprobe`
4. Config override in the FfprobePath field

**Manual fix:** Set the **FFprobe Path** field in Settings to the correct path.

*Applies to: v0.0.0.95 and earlier. Fixed in v0.0.0.96+.*

## "no GPU found"

**Symptom:** whisper.log shows `whisper_backend_init_gpu: no GPU found`.

**Cause:** Either:
1. The plugin is using the CPU binary (expected — CPU binary says this)
2. The container doesn't have GPU access

**Check:**
- Config page shows **Runtime Hardware Status** (should be "cuda")
- CUDA binary is deployed (config shows "✓ deployed and ready")
- Container has `--gpus all` or nvidia-container-toolkit configured
- `docker exec jellyfin nvidia-smi` works

## Binary Fails to Deploy

**Symptom:** "Bundled binary not found" in logs or config page shows "System Execution Binary Absent"

**Cause:** Plugin zip was extracted incorrectly or the binary isn't in the expected path.

**Plugin directory structure (expected):**
```
plugins/Whisper Subtitles_0.0.0.98/
├── Jellyfin.Plugin.WhisperSubtitles.dll
├── Jellyfin.Plugin.WhisperSubtitles.dll
├── whisper/
│   └── linux-x64/
│       ├── whisper-whisper-cli
│       ├── whisper-whisper-cli-cuda
│       ├── libcudart.so.12
│       ├── libcublas.so.12
│       └── libcublasLt.so.12
└── ...
```

**Fix:** Reinstall the plugin. If manually extracting, ensure the `whisper/` directory is inside the plugin folder.

## Plugin Config Page Shows Diagnostic Info

The config page shows diagnostics including:
- Whether the binary is deployed
- GPU type detected
- Number of models cached
- Available CPU threads

If diagnostics show "unconfigured," run the **Install Binary** button or trigger the task once (it auto-deploys).

## Progress Stuck at 0%

**Symptom:** The scheduled task shows 0% for a long time.

**Cause:** Before v0.0.0.97, progress was reported per-video. A single video chunked into 6 parts would show 0% for 7+ minutes.

**Fix:** v0.0.0.97+ reports progress per-chunk within each video.

## Deploying a New Version

When upgrading, always clear the old binary cache:

```bash
rm -rf /cache/whisper-cpp/
```

Otherwise the old binary is used even with the new plugin installed.
