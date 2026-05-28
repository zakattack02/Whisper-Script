# Installation

## Prerequisites

- **Jellyfin 10.11.x** (other versions may work but 10.11.2 is the target ABI)
- **Linux x86_64** server (the binary is compiled for linux-x64)
- **~750MB free disk space** for the plugin zip (v0.0.0.98+ includes the CUDA binary)
- **Additional disk space** for downloaded model files (75MB for Tiny, up to 3GB for Large)

## Method 1: Repository Manifest

Add the plugin repository to Jellyfin:

1. Dashboard → Plugins → Repositories → **Add**
2. URL: `https://github.com/zakattack02/Whisper-Script/raw/refs/heads/feature/jellyfin-plugin/manifest.json`
3. Name: `Whisper Subtitles`
4. Click **Save**

Then: Catalog → Find "Whisper Subtitles" → Install → Restart Jellyfin.

## Method 2: Manual ZIP Upload

1. Download the latest release zip from [GitHub Releases](https://github.com/zakattack02/Whisper-Script/releases)
2. Dashboard → Plugins → **Manual Install** → Browse to the zip → Upload
3. Restart Jellyfin

## Method 3: Manual Filesystem Deployment

Extract the zip directly into the Jellyfin plugins directory:

```bash
# Find your plugin directory (usually one of these)
ls /usr/lib/jellyfin/plugins/
ls /var/lib/jellyfin/plugins/
# or check from Jellyfin Dashboard → About

# Extract the plugin
sudo unzip jellyfin-plugin-whispersubtitles_0.0.0.98.zip \
    -d /var/lib/jellyfin/plugins/Whisper\ Subtitles_0.0.0.98/

# Fix permissions
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/Whisper\ Subtitles_0.0.0.98/

# Restart Jellyfin
sudo systemctl restart jellyfin
```

## Post-Install

After restart, the plugin needs to:

1. **Copy the binary** from the plugin bundle to the cache directory (`~/.cache/whisper-cpp/`)
2. **Download the model** (if you click "Download Model" or when the task first runs)

> On first use, the plugin automatically deploys the binary from its bundle. You do NOT need to manually copy anything. The cache is at `$JELLYFIN_CACHE_DIR/whisper-cpp/` or `~/.cache/whisper-cpp/`.

## Upgrading

When upgrading, delete the old binary cache so the new binary is deployed:

```bash
rm -rf /cache/whisper-cpp/
# or
rm -rf ~/.cache/whisper-cpp/
```

Then install the new plugin version and restart Jellyfin.

## Docker Considerations

If running Jellyfin in Docker:

```bash
# Ensure the cache directory persists
docker run -d \
    --name jellyfin \
    -v /path/to/cache:/cache \
    # ... other volumes
    jellyfin/jellyfin

# For GPU support, add:
docker run -d \
    --gpus all \
    # ... or use nvidia-container-toolkit
```

The plugin uses `JELLYFIN_CACHE_DIR` environment variable if set, otherwise `~/.cache/`.
