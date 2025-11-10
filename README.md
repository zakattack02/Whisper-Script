# Jellyfin Whisper Subtitles Plugin# Whisper Subtitle Generator

> **Note:** For the standalone Python CLI version, see the [`main` branch](https://github.com/zakattack02/Whisper-Script/tree/main)> **📌 Repository Structure**

Automatically generate subtitles for your Jellyfin media library using OpenAI's Whisper AI. This is a native C# plugin that integrates directly with Jellyfin.> - **`feature/jellyfin-plugin` branch**: Jellyfin plugin with web UI (in development)

> 

## 🚧 Status: In Development> Switch branches: `git checkout feature/jellyfin-plugin`



This plugin is currently under active development. The foundation is complete and the project compiles, but subtitle generation functionality is still being implemented.---



### ✅ CompletedAutomatically generate subtitles for your Jellyfin/Plex media library using OpenAI's Whisper AI. Perfect for anime, foreign films, and any video content that needs subtitles.

- Project structure with .NET 8.0

- Jellyfin plugin integration## Features

- Whisper.NET library integration

- Configuration system with all settings- **Translation** - Translate any language to English

- Professional web UI for configuration- **Transcription** - Generate subtitles in the original language

- Successfully compiles- **GPU Acceleration** - Automatic CUDA support for fast processing

- **Batch Processing** - Process entire folders or specific files

### 🔨 In Progress- **Smart Detection** - Skip videos that already have subtitles

- WhisperService implementation (core subtitle generation)- **AI Identifier** - Mark AI-generated subtitles (e.g., `video.en.whisper.srt`)

- Scheduled task integration- **Flexible Models** - Choose from 5 model sizes (tiny to large)

- Library post-scan support- **Multiple Formats** - SRT, VTT, TXT, JSON output

- Subtitle detection logic- **Regeneration** - Re-process files with better models

- **Long Filename Support** - Handles filesystem limits gracefully

## Features (When Complete)

## Requirements

- **Native Jellyfin Integration** - Works directly within Jellyfin's plugin system

- **Scheduled Tasks** - Generate subtitles on schedule or manually- Python 3.8+

- **Auto-Processing** - Optional automatic generation after library scans- FFmpeg (for audio extraction)

- **GPU Acceleration** - Uses your GPU via Whisper.NET for fast processing- CUDA-capable GPU (optional, but highly recommended for speed)

- **Multiple Models** - Choose from tiny, base, small, medium, turbo, or large

- **Translation Support** - Translate any language to English or transcribe in original## Quick Start

- **AI Identifier** - Mark AI-generated subtitles (e.g., `video.en.whisper.srt`)

- **Word Timestamps** - Optional word-level timing for precise subtitles### 1. Install Dependencies

- **Smart Detection** - Skip files that already have subtitles

- **Library Selection** - Choose which libraries to process```bash

# Create virtual environment

## Requirementspython -m venv .venv

source .venv/bin/activate  # On Windows: .venv\Scripts\activate

- Jellyfin Server 10.8.x or higher

- .NET 8.0 Runtime (included with Jellyfin)# Install requirements

- Optional: CUDA-capable GPU for faster processingpip install -U openai-whisper torch tqdm

```

## Building from Source

### 2. Configure Your Folders

### Prerequisites

Edit `batch_generate.py` and add your media folders:

```bash

# Install .NET SDK 8.0```python

# Arch LinuxDEFAULT_MEDIA_FOLDERS = [

sudo pacman -S dotnet-sdk-8.0    "/mnt/jellyfin/anime/",

    "/mnt/jellyfin/movies/",

# Ubuntu/Debian    "/path/to/tv-shows/",

sudo apt install dotnet-sdk-8.0]

```

# Or download from https://dotnet.microsoft.com/download

```### 3. Run the Script



### Build```bash

# Activate virtual environment

```bashsource .venv/bin/activate

cd Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles

dotnet build -c Release# Process configured folders

```python batch_generate.py



The compiled DLL will be in `bin/Release/net8.0/Jellyfin.Plugin.WhisperSubtitles.dll`# Or process a specific folder

python batch_generate.py /path/to/media

## Installation (When Ready)```



1. Build the plugin (see above)## Usage

2. Copy the DLL to your Jellyfin plugins folder:

   - Linux: `~/.local/share/jellyfin/plugins/WhisperSubtitles/`### Basic Commands

   - Windows: `%AppData%\Jellyfin\Server\plugins\WhisperSubtitles\`

3. Restart Jellyfin```bash

4. Configure via Dashboard → Plugins → Whisper Subtitles# Process configured folders with defaults

python batch_generate.py

## Configuration

# Process specific folder

All settings are available through the Jellyfin web interface:python batch_generate.py /mnt/jellyfin/anime



### Model Settings# Process multiple folders

- **Whisper Model**: Choose model size (tiny to large, plus turbo)python batch_generate.py /media/anime /media/movies

  - `tiny` - ~10x speed, ~1GB VRAM (fastest)

  - `base` - ~7x speed, ~1GB VRAM# Dry run (preview without generating)

  - `small` - ~4x speed, ~2GB VRAM (recommended)python batch_generate.py /media --dry-run

  - `medium` - ~2x speed, ~5GB VRAM

  - `turbo` - ~8x speed, ~6GB VRAM (great balance)# Show all options

  - `large` - 1x speed, ~10GB VRAM (best quality)python batch_generate.py --help

```

### Language Settings

- **Target Language**: Language code (en, es, fr, de, ja, ko, zh, etc.)## Common Use Cases

- **Translate to English**: Convert any language to English

- **AI Identifier**: String added to filenames (default: "whisper")#### 1. Translate Japanese Anime to English



### Processing Options```bash

- **Word Timestamps**: Enable word-level timing (slower but more precise)python batch_generate.py /mnt/jellyfin/anime \

- **Process on Library Scan**: Auto-generate for new media  --model small \

- **Skip Existing**: Don't process files with subtitles  --translate \

- **Regenerate AI**: Re-process AI-generated subtitles  --identifier whisper

```

## Project Structure

**Result:** `anime_episode.en.whisper.srt`

```

Jellyfin.Plugin.WhisperSubtitles/#### 2. Transcribe English Content

├── Configuration/

│   ├── PluginConfiguration.cs      # Configuration class```bash

│   └── configPage.html             # Web UIpython batch_generate.py /mnt/jellyfin/movies \

├── Services/                        # (To be implemented)  --model base \

│   ├── IWhisperService.cs  --no-translate \

│   ├── WhisperService.cs  --language en

│   └── SubtitleDetectionService.cs```

├── Tasks/                           # (To be implemented)

│   ├── WhisperSubtitleTask.cs**Result:** `movie.en.whisper.srt`

│   └── WhisperPostScanTask.cs

└── Plugin.cs                        # Main plugin entry point#### 3. Generate Spanish Subtitles

```

```bash

## Developmentpython batch_generate.py /media/spanish \

  --no-translate \

### Architecture  --language es

```

This plugin uses:

- **Whisper.NET** - C# bindings for OpenAI Whisper**Result:** `video.es.whisper.srt`

- **Jellyfin.Model** - Jellyfin's core models and interfaces

- **Jellyfin.Controller** - Jellyfin's controller abstractions#### 4. Regenerate with Better Model



### Next Steps```bash

# First run created subtitles with 'base' model

1. Implement `WhisperService` - Core logic to process videos with Whisper.NET# Upgrade to 'small' model:

2. Implement `WhisperSubtitleTask` - Scheduled task for manual generationpython batch_generate.py /media \

3. Implement `SubtitleDetectionService` - Check for existing subtitles  --model small \

4. Implement `WhisperPostScanTask` - Auto-process after library scans  --regenerate-ai

5. Add comprehensive error handling and logging```

6. Testing with real Jellyfin instance

#### 5. No AI Identifier (Clean Names)

### Contributing

```bash

Contributions are welcome! Areas that need work:python batch_generate.py /media \

- Core subtitle generation implementation  --identifier ""

- Error handling and logging```

- Testing and bug fixes

- Documentation improvements**Result:** `movie.en.srt`



## Documentation## Configuration Options



- [Development Plan](JELLYFIN_PLUGIN_PLAN.md) - Detailed development roadmap### Model Selection (`--model`, `-m`)

- [Setup Complete](SETUP_COMPLETE.md) - What's been accomplished

- [Jellyfin Plugin Docs](https://jellyfin.org/docs/general/server/plugins/)Choose the Whisper model based on your needs:

- [Whisper.NET](https://github.com/sandrohanea/whisper.net)

| Model    | Parameters | VRAM   | Relative Speed | Best For |

## Comparison with Python CLI|----------|-----------|--------|----------------|----------|

| `tiny`   | 39M       | ~1 GB  | ~10x (fastest) | Quick testing, very fast processing |

| Feature | Python CLI (main branch) | C# Plugin (this branch) || `base`   | 74M       | ~1 GB  | ~7x            | Testing, decent speed |

|---------|-------------------------|-------------------------|| `small`  | 244M      | ~2 GB  | ~4x            | **Recommended** - good balance |

| Jellyfin Integration | ❌ External | ✅ Native || `medium` | 769M      | ~5 GB  | ~2x            | High quality, slower |

| GUI Configuration | ❌ Command-line only | ✅ Web UI || `turbo`  | 809M      | ~6 GB  | ~8x            | Fast with good quality (newer) |

| Scheduled Tasks | ❌ Manual cron/systemd | ✅ Jellyfin scheduler || `large`  | 1550M     | ~10 GB | 1x (slowest)   | Best quality, professional use |

| Auto-processing | ❌ Manual | ✅ Post-scan hook |

| Subtitle Generation | ✅ Working | 🚧 In progress |**Default:** `small`

| GPU Support | ✅ Working | ✅ Via Whisper.NET |

**Note:** Speed is relative to the `large` model. `turbo` is a newer optimized model that's faster than `small` with quality close to `large`.

## Why Two Versions?

```bash

- **Python CLI (main branch)**: Standalone tool, works with any media server, flexible command-line interfacepython batch_generate.py /media --model tiny    # Fastest (10x)

- **C# Plugin (this branch)**: Deep Jellyfin integration, automated workflows, web-based configurationpython batch_generate.py /media --model turbo   # Fast + quality (8x, newer)

python batch_generate.py /media --model base    # Fast (7x)

Both serve different use cases!python batch_generate.py /media --model small   # Recommended (4x)

python batch_generate.py /media --model medium  # High quality (2x)

## Licensepython batch_generate.py /media --model large   # Best quality (1x)

```

GPL-3.0 - Required by Jellyfin's plugin system as plugins link against GPL-licensed Jellyfin libraries.

### Output Format (`--format`, `-f`)

## Credits

**Default:** `srt`

- [OpenAI Whisper](https://github.com/openai/whisper) - The amazing AI model

- [Whisper.NET](https://github.com/sandrohanea/whisper.net) - C# bindings```bash

- [Jellyfin](https://jellyfin.org/) - The free media serverpython batch_generate.py /media --format srt   # SubRip (most compatible)

python batch_generate.py /media --format vtt   # WebVTT

---python batch_generate.py /media --format txt   # Plain text

python batch_generate.py /media --format json  # JSON format

**Development Status:** Foundation complete, core functionality in progress. Watch this repo for updates!```


### Language (`--language`, `-l`)

**Default:** `en` (English)

```bash
python batch_generate.py /media --language en  # English
python batch_generate.py /media --language es  # Spanish
python batch_generate.py /media --language fr  # French
python batch_generate.py /media --language de  # German
python batch_generate.py /media --language ja  # Japanese
python batch_generate.py /media --language ko  # Korean
python batch_generate.py /media --language zh  # Chinese
python batch_generate.py /media --language ru  # Russian
python batch_generate.py /media --language pt  # Portuguese
python batch_generate.py /media --language it  # Italian
```

[Full list of supported language codes](https://github.com/openai/whisper#available-models-and-languages)

### Translation vs Transcription

**Translation** - Convert any language to English:

```bash
python batch_generate.py /media --translate
```

**Transcription** - Keep original language:

```bash
python batch_generate.py /media --no-translate --language ja
```

**Default:** Translation enabled (set in script configuration)

### AI Identifier (`--identifier`, `-i`)

Mark AI-generated subtitles with an identifier:

**Default:** `whisper`

```bash
python batch_generate.py /media --identifier whisper
# Result: video.en.whisper.srt

python batch_generate.py /media --identifier ai
# Result: video.en.ai.srt

python batch_generate.py /media --identifier auto
# Result: video.en.auto.srt

python batch_generate.py /media --identifier ""
# Result: video.en.srt (no identifier)
```

### Processing Options

**Regenerate AI Subtitles** - Re-process files that already have AI-generated subtitles:

```bash
python batch_generate.py /media --regenerate-ai
```

**Skip/Don't Skip Existing** - Control whether to skip files with existing subtitles:

```bash
python batch_generate.py /media --skip-existing      # Skip (default)
python batch_generate.py /media --no-skip-existing   # Process all
```

**Dry Run** - Preview what would be processed:

```bash
python batch_generate.py /media --dry-run
```

## Output File Naming

### Naming Format

```
video_name.{language}.{identifier}.{format}
```

### Examples

| Configuration | Output Filename |
|--------------|-----------------|
| Default (translate, identifier: whisper) | `anime.en.whisper.srt` |
| No identifier | `movie.en.srt` |
| Spanish transcription | `video.es.whisper.srt` |
| Custom identifier (ai) | `show.en.ai.srt` |
| VTT format | `film.en.whisper.vtt` |

### Jellyfin/Plex Compatibility

All naming formats are compatible with Jellyfin and Plex:

✅ `video.srt`  
✅ `video.en.srt`  
✅ `video.en.whisper.srt`  
✅ `video.eng.srt`  
✅ `video.en.forced.srt`

## Real-World Examples

### Example 1: Anime Library

**Scenario:** You have a large anime collection in Japanese and want English subtitles.

```bash
# Configure in script
DEFAULT_MEDIA_FOLDERS = ["/mnt/jellyfin/anime/"]
DEFAULT_MODEL = "small"
DEFAULT_TRANSLATE = True
DEFAULT_IDENTIFIER = "whisper"

# Run
python batch_generate.py
```

**Result:**
```
Attack on Titan/
├── S01E01.mkv
├── S01E01.en.whisper.srt  ← Generated
├── S01E02.mkv
└── S01E02.en.whisper.srt  ← Generated
```

### Example 2: English Movie Collection

**Scenario:** Generate subtitles for English movies without translation.

```bash
python batch_generate.py /mnt/jellyfin/movies \
  --model base \
  --no-translate \
  --identifier ""
```

**Result:**
```
Movies/
├── Inception.mkv
├── Inception.en.srt       ← Clean filename
├── The Matrix.mkv
└── The Matrix.en.srt      ← Clean filename
```

### Example 3: Multi-Language Library

**Scenario:** Transcribe Spanish content in Spanish.

```bash
python batch_generate.py /media/spanish \
  --no-translate \
  --language es \
  --model small
```

**Result:**
```
spanish/
├── La Casa de Papel S01E01.mkv
└── La Casa de Papel S01E01.es.whisper.srt  ← Spanish subtitles
```

### Example 4: Upgrade Subtitle Quality

**Scenario:** You previously generated subtitles with 'base' model, now upgrade to 'small'.

```bash
python batch_generate.py /media \
  --model small \
  --regenerate-ai
```

This will re-process only files with AI-generated subtitles.

## Performance

### Processing Speed (with RTX 3080 Ti)

| Content | Model | Time per Minute of Video | Notes |
|---------|-------|--------------------------|-------|
| 22-min anime | tiny | ~30 seconds | Very fast but lower quality |
| 22-min anime | base | ~2-3 minutes | Good for quick processing |
| 22-min anime | small | ~3-5 minutes | **Recommended balance** |
| 22-min anime | medium | ~6-10 minutes | Better quality |
| 22-min anime | turbo | ~2-3 minutes | Fast with near-large quality |
| 90-min movie | small | ~12-18 minutes | |
| 90-min movie | medium | ~18-36 minutes | |
| 90-min movie | turbo | ~9-15 minutes | Great for movies |

**CPU Processing:** 10-20x slower than GPU

**Turbo Model:** Newer optimized model (late 2023+) - nearly as accurate as `large` but ~8x faster. Great choice if available.

### Tips for Large Libraries

1. **Use `screen` or `tmux`** for long-running sessions:
   ```bash
   screen -S subtitles
   source .venv/bin/activate
   python batch_generate.py
   # Detach: Ctrl+A, then D
   # Reattach: screen -r subtitles
   ```

2. **Start with dry-run** to see what will be processed:
   ```bash
   python batch_generate.py --dry-run
   ```

3. **Process overnight** for very large libraries

4. **Use appropriate model** - `base` for speed, `small` for quality

5. **Monitor first few files** to ensure quality is acceptable

## Troubleshooting

### GPU Not Detected

```bash
# Check GPU availability
python -c "import torch; print('GPU:', torch.cuda.is_available())"

# If False, install CUDA-enabled PyTorch
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
```

### Out of Memory Errors

Use a smaller model:
```bash
python batch_generate.py /media --model tiny
# or
python batch_generate.py /media --model base
```

### Subtitles Not Appearing in Jellyfin

1. **Refresh metadata** in Jellyfin/Plex
2. **Check file location** - subtitle must be in same folder as video
3. **Verify naming** - ensure subtitle matches video filename
4. **Use SRT format** - most compatible: `--format srt`

### Slow Processing on CPU

- Install CUDA-enabled PyTorch for GPU acceleration
- Use smaller model (`tiny` or `base`)
- Process fewer files at once
- Consider cloud GPU instances for one-time batch jobs

## Customization

### Editing Default Configuration

Open `batch_generate.py` and modify the DEFAULT_* variables:

```python
DEFAULT_MEDIA_FOLDERS = [
    "/your/media/folder/",
]

DEFAULT_MODEL = "small"           # Change default model
DEFAULT_FORMAT = "srt"            # Change default format
DEFAULT_LANGUAGE = "en"           # Change default language
DEFAULT_TRANSLATE = True          # Enable/disable translation
DEFAULT_IDENTIFIER = "whisper"    # Change identifier
DEFAULT_REGENERATE_AI = False     # Regenerate AI subs by default
```

### Supported Video Formats

MP4, MKV, AVI, MOV, WMV, FLV, WebM, M4V, MPG, MPEG

### Supported Subtitle Formats

The script checks for existing subtitles in these formats:
SRT, VTT, ASS, SSA, SUB

## Command Reference

### All Options

```
usage: batch_generate.py [-h] [--model {tiny,base,small,medium,large}]
                        [--format {srt,vtt,txt,json}] [--language LANGUAGE]
                        [--translate] [--no-translate] [--identifier IDENTIFIER]
                        [--regenerate-ai] [--skip-existing] [--no-skip-existing]
                        [--dry-run]
                        [folders ...]

positional arguments:
  folders               Media folders to process

options:
  -h, --help            show this help message and exit
  --model, -m           Whisper model size
  --format, -f          Subtitle format
  --language, -l        Target language code
  --translate, -t       Translate audio to English
  --no-translate        Transcribe in original language
  --identifier, -i      AI subtitle identifier
  --regenerate-ai       Regenerate AI-generated subtitles
  --skip-existing       Skip files with existing subtitles
  --no-skip-existing    Process all files
  --dry-run, -n         Preview without generating
```

### Short Options

| Short | Long | Description |
|-------|------|-------------|
| `-m` | `--model` | Model size |
| `-f` | `--format` | Output format |
| `-l` | `--language` | Language code |
| `-t` | `--translate` | Enable translation |
| `-i` | `--identifier` | AI identifier |
| `-n` | `--dry-run` | Dry run mode |

## License

This script uses OpenAI's Whisper model. See [Whisper License](https://github.com/openai/whisper/blob/main/LICENSE) for details.

## Credits

- [OpenAI Whisper](https://github.com/openai/whisper) - The amazing AI model
- [FFmpeg](https://ffmpeg.org/) - Audio extraction

---

**Made for Jellyfin/Plex Media Servers**
