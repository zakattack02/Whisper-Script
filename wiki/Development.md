# Development

## Repository Structure

```
├── Directory.Build.props           — Version number, assembly metadata
├── make-release.sh                  — Full release orchestrator
├── manifest.json                    — Plugin repository manifest
├── releases/                        — Built zips
├── wiki/                            — Wiki documentation
│
├── Jellyfin.Plugin.WhisperSubtitles/
│   ├── Jellyfin.Plugin.WhisperSubtitles/
│   │   ├── Plugin.cs                — Plugin entry point (Registrator)
│   │   ├── Jellyfin.Plugin.WhisperSubtitles.csproj
│   │   ├── Configuration/
│   │   │   ├── PluginConfiguration.cs
│   │   │   └── configPage.html
│   │   ├── Controllers/
│   │   │   └── WhisperSubtitlesController.cs
│   │   ├── Services/
│   │   │   ├── IWhisperService.cs
│   │   │   ├── WhisperService.cs
│   │   │   ├── WhisperBinaryManager.cs
│   │   │   ├── ISubtitleDetectionService.cs
│   │   │   └── SubtitleDetectionService.cs
│   │   └── Tasks/
│   │       ├── WhisperSubtitleTask.cs
│   │       └── WhisperPostScanTask.cs
│   │
│   └── Scripts/
│       ├── Dockerfile.whisper        — Multi-stage CPU+CUDA build
│       └── Build-whisper.sh          — Build orchestrator script
```

## Key Classes

### Plugin.cs
Entry point. Registers services with the Jellyfin DI container. Exposes `Plugin.Instance` singleton.

### WhisperBinaryManager.cs
- Binary discovery and deployment
- GPU detection (nvidia-smi, vulkaninfo)
- FFmpeg/ffprobe path discovery
- CUDA binary and .so file deployment

### WhisperService.cs
- Orchestrates the full subtitle generation pipeline
- Audio extraction via FFmpeg
- Chunking decision (duration-based)
- whisper-cli invocation and result handling
- SRT chunk merging

### WhisperSubtitleTask.cs
- Scheduled task that processes videos in configured libraries
- Progress reporting wrapper for per-video sub-progress
- Library filtering using collection folder IDs
- Skip logic (existing subtitles, AI regeneration)

### WhisperSubtitlesController.cs
- REST API endpoints used by the config page
- BinaryStatus, InstallBinary, DownloadModel, stats

## Build Scripts

### make-release.sh

The main release script:

```
Step 1: Update version in Directory.Build.props
Step 2: Clean previous builds
Step 3: Build whisper.cpp (Docker → both CPU + CUDA binaries)
Step 4: Verify binaries
Step 5: Build C# plugin (dotnet publish)
Step 6: Package zip
Step 7: Update manifest.json
Step 8: GitHub release (commit, tag, push, gh release create)
```

### Build-whisper.sh

Builds whisper.cpp. Supports two modes:
- **Docker** (default) — uses multi-stage Dockerfile for GLIBC compatibility
- **Native** (`--no-docker`) — builds on host (warning: GLIBC version depends on host)

### Dockerfile.whisper

Multi-stage build producing both binaries. Key details:
- CPU stage: `ubuntu:22.04`, `-DGGML_CUDA=OFF`
- CUDA stage: `nvidia/cuda:12.4.1-devel-ubuntu22.04`, `-DGGML_CUDA=ON`, 7 GPU architectures
- Output stage: copies all artifacts + CUDA .so files

## Code Conventions

- **No XML docs on implementation** — only on interfaces and public API surfaces
- **ILogger pattern** — all services take `ILogger<T>` via constructor injection
- **Async all the way** — `Task<bool>` return types, `CancellationToken` parameters
- **Process management** — `ProcessStartInfo` for FFmpeg and whisper-cli, async stdout/stderr reading
- **Configuration** — `PluginConfiguration` extends `BasePluginConfiguration`, loaded via Jellyfin API
- **Versioning** — 4-part version in `Directory.Build.props`, updated by make-release.sh

## Adding a New Feature

1. Add config properties to `PluginConfiguration.cs`
2. Add UI fields to `configPage.html` (load/save in JS)
3. Implement logic in the appropriate service
4. Wire up in the task or controller
5. Update `make-release.sh` if new build artifacts are needed
6. Update `Directory.Build.props` version
7. Run `make-release.sh` to build and release

## Release Checklist

- [ ] All changes committed and tested
- [ ] `dotnet build` succeeds with no warnings
- [ ] whisper.cpp builds in Docker (CPU + CUDA)
- [ ] Binary GLIBC compatibility checked (should be ≤ 2.34)
- [ ] CUDA binary + .so files present in output
- [ ] `make-release.sh` completes successfully
- [ ] GitHub release created with correct zip and tag
- [ ] manifest.json updated with new version entry
- [ ] Test on remote server (deploy + run task)
