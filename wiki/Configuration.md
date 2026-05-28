# Configuration

Access the config page at Dashboard → Plugins → Whisper Subtitles → Settings.

## Model & Engine

### Whisper Model

The Whisper model determines accuracy vs. speed tradeoff:

| Model | Size | Speed (vs Large) | VRAM | Quality |
|---|---|---|---|---|
| Tiny | 75 MB | ~10x | ~1 GB | Lowest |
| Tiny.en | 75 MB | ~10x | ~1 GB | English-only Tiny |
| Base | 140 MB | ~7x | ~1 GB | Low |
| Base.en | 140 MB | ~7x | ~1 GB | English-only Base |
| **Small** | **460 MB** | **~4x** | **~2 GB** | **Recommended** |
| Small.en | 460 MB | ~4x | ~2 GB | English-only Small |
| Medium | 1.5 GB | ~2x | ~5 GB | High |
| Medium.en | 1.5 GB | ~2x | ~5 GB | English-only Medium |
| Turbo | 1.6 GB | ~8x | ~6 GB | High (fast) |
| Large (v3) | 3 GB | 1x | ~10 GB | Best |

> **Small** is the default and recommended starting point. Turbo is nearly as accurate as Large but 8x faster.

### Download Model

Click **Download Model** to pre-download the selected model. This happens automatically when the task runs, but pre-downloading lets you verify the download succeeded.

### Target Language

Language code for subtitles. Common values:

- `en` — English
- `es` — Spanish
- `fr` — French
- `de` — German
- `ja` — Japanese
- `zh` — Chinese
- `auto` — auto-detect (slower)

### AI Identifier

A tag appended to subtitle filenames so they can be identified as AI-generated:

```
Movie Name (2024).en.whisper.srt
                      ↑ identifier
```

Default: `whisper`. Set to empty to disable tagging.

## Acceleration

### Enable CUDA (NVIDIA GPU)

When checked, the plugin uses the CUDA GPU binary instead of the CPU binary.

**Prerequisites:**
1. Jellyfin container must have `--gpus all` or nvidia-container-toolkit configured
2. The CUDA binary (`whisper-whisper-cli-cuda`) must be deployed in the cache
3. NVIDIA drivers must be installed on the host

The config page shows:
- **Runtime Hardware Status** — detected GPU type (cuda, vulkan, metal, or none)
- **CUDA Binary** — whether the CUDA binary is deployed and ready

See [GPU Acceleration](GPU-Acceleration) for full setup details.

### FFprobe Path

Custom path to the `ffprobe` binary. Used to measure audio duration for chunked processing.

Leave empty for auto-detection. The plugin searches:
1. Next to the found `ffmpeg` binary
2. Known container paths (`/usr/lib/jellyfin-ffmpeg/ffprobe`, etc.)
3. `which ffprobe` via PATH
4. Config override (this field)

## Library Automation

### Process on Library Scan

When enabled, the plugin hooks into Jellyfin's library scan and generates subtitles for new media as it's discovered.

### Skip Existing Subtitles

Skip videos that already have any subtitle track. Does NOT skip videos with only AI-generated subtitles (see Regenerate).

### Regenerate AI Subtitles

Force-regenerate subtitles even if an AI-tagged subtitle already exists. Useful when upgrading models or changing settings.

### Translate to English

Translate non-English audio to English subtitles. When unchecked, subtitles match the spoken language.

### Enable Word-Level Timestamps

Produces more precise subtitle timing with word-level alignment. **Significantly increases processing time** (roughly 2-3x slower).

### Show in Main Menu

Toggle the plugin entry in Jellyfin's main navigation sidebar.

### Libraries to Process

Select which media libraries to scan. Leave empty to process all libraries.

### Folders to Exclude

List absolute paths to exclude from processing (one per line).

```
/Media/Home Videos/Kids Stuff
/Media/Movies/Sample
```
