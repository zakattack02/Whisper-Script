# Whisper Subtitles Plugin for Jellyfin

Generate AI-powered subtitles for your media library — completely local, no external API calls.

Built on [whisper.cpp](https://github.com/ggerganov/whisper.cpp), this plugin runs the Whisper model directly on your Jellyfin server, supporting both CPU and NVIDIA GPU (CUDA) acceleration.

## Features

- **Fully local** — no data leaves your server, no API keys, no subscription
- **Multiple models** — Tiny through Large, plus Turbo for speed/quality balance
- **CPU & GPU** — runs on CPU with AVX2, or NVIDIA GPU via CUDA
- **Batch processing** — scheduled task processes your entire library
- **Library scan hook** — auto-generate subtitles for new media
- **Chunked processing** — handles long videos (3+ hours) without OOM
- **Progress reporting** — per-chunk progress for the scheduled task UI
- **SRT output** — standard subtitle format compatible with all clients
- **Translation** — translate any language to English
- **Word timestamps** — word-level timing for fine subtitle sync
- **AI identifier tags** — mark generated subtitles for easy identification

## Quick Start

1. Install the plugin via [manifest or manual zip](Installation)
2. Go to Dashboard → Plugins → Whisper Subtitles
3. Select a model (Small recommended) and target language
4. Click **Download Model** (first run only — downloads ~460MB)
5. Click **Save** at the bottom
6. Go to Dashboard → Scheduled Tasks → **Generate Whisper Subtitles** → Run

> **First run will download the whisper model to `~/.cache/whisper/` and deploy the binary to `~/.cache/whisper-cpp/`. This happens automatically.**

## Table of Contents

| Page | Description |
|---|---|
| [Installation](Installation) | Install via manifest, zip, or manual deployment |
| [Configuration](Configuration) | All settings explained |
| [Usage](Usage) | Running tasks, interpreting results |
| [Architecture](Architecture) | How the plugin works internally |
| [GPU Acceleration](GPU-Acceleration) | CUDA setup and dual-binary system |
| [Build from Source](Build-from-Source) | Building whisper.cpp + plugin |
| [Troubleshooting](Troubleshooting) | Common issues and solutions |
| [Version History](Version-History) | Changelog of all releases |
| [Development](Development) | Code structure and contributing |
