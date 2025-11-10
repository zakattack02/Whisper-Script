# Whisper Subtitle Generator for Jellyfin

Automatically generate subtitles for your Jellyfin/Plex media library using OpenAI's Whisper AI. Perfect for anime, foreign films, and any video content that needs subtitles.

## Features

- **Translation** - Translate any language to English
- **Transcription** - Generate subtitles in the original language
- **GPU Acceleration** - Automatic CUDA support for fast processing
- **Batch Processing** - Process entire folders or specific files
- **Smart Detection** - Skip videos that already have subtitles
- **AI Identifier** - Mark AI-generated subtitles (e.g., `video.en.whisper.srt`)
- **Flexible Models** - Choose from 5 model sizes (tiny to large)
- **Multiple Formats** - SRT, VTT, TXT, JSON output
- **Regeneration** - Re-process files with better models
- **Long Filename Support** - Handles filesystem limits gracefully

## Requirements

- Python 3.8+
- FFmpeg (for audio extraction)
- CUDA-capable GPU (optional, but highly recommended for speed)

## Quick Start

### 1. Install Dependencies

```bash
# Create virtual environment
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate

# Install requirements
pip install -U openai-whisper torch tqdm
```

### 2. Configure Your Folders

Edit `batch_generate.py` and add your media folders:

```python
DEFAULT_MEDIA_FOLDERS = [
    "/mnt/jellyfin/anime/",
    "/mnt/jellyfin/movies/",
    "/path/to/tv-shows/",
]
```

### 3. Run the Script

```bash
# Activate virtual environment
source .venv/bin/activate

# Process configured folders
python batch_generate.py

# Or process a specific folder
python batch_generate.py /path/to/media
```

## Usage

### Basic Commands

```bash
# Process configured folders with defaults
python batch_generate.py

# Process specific folder
python batch_generate.py /mnt/jellyfin/anime

# Process multiple folders
python batch_generate.py /media/anime /media/movies

# Dry run (preview without generating)
python batch_generate.py /media --dry-run

# Show all options
python batch_generate.py --help
```

## Common Use Cases

#### 1. Translate Japanese Anime to English

```bash
python batch_generate.py /mnt/jellyfin/anime \
  --model small \
  --translate \
  --identifier whisper
```

**Result:** `anime_episode.en.whisper.srt`

#### 2. Transcribe English Content

```bash
python batch_generate.py /mnt/jellyfin/movies \
  --model base \
  --no-translate \
  --language en
```

**Result:** `movie.en.whisper.srt`

#### 3. Generate Spanish Subtitles

```bash
python batch_generate.py /media/spanish \
  --no-translate \
  --language es
```

**Result:** `video.es.whisper.srt`

#### 4. Regenerate with Better Model

```bash
# First run created subtitles with 'base' model
# Upgrade to 'small' model:
python batch_generate.py /media \
  --model small \
  --regenerate-ai
```

#### 5. No AI Identifier (Clean Names)

```bash
python batch_generate.py /media \
  --identifier ""
```

**Result:** `movie.en.srt`

## Configuration Options

### Model Selection (`--model`, `-m`)

Choose the Whisper model based on your needs:

| Model    | Parameters | VRAM   | Relative Speed | Best For |
|----------|-----------|--------|----------------|----------|
| `tiny`   | 39M       | ~1 GB  | ~10x (fastest) | Quick testing, very fast processing |
| `base`   | 74M       | ~1 GB  | ~7x            | Testing, decent speed |
| `small`  | 244M      | ~2 GB  | ~4x            | **Recommended** - good balance |
| `medium` | 769M      | ~5 GB  | ~2x            | High quality, slower |
| `turbo`  | 809M      | ~6 GB  | ~8x            | Fast with good quality (newer) |
| `large`  | 1550M     | ~10 GB | 1x (slowest)   | Best quality, professional use |

**Default:** `small`

**Note:** Speed is relative to the `large` model. `turbo` is a newer optimized model that's faster than `small` with quality close to `large`.

```bash
python batch_generate.py /media --model tiny    # Fastest (10x)
python batch_generate.py /media --model turbo   # Fast + quality (8x, newer)
python batch_generate.py /media --model base    # Fast (7x)
python batch_generate.py /media --model small   # Recommended (4x)
python batch_generate.py /media --model medium  # High quality (2x)
python batch_generate.py /media --model large   # Best quality (1x)
```

### Output Format (`--format`, `-f`)

**Default:** `srt`

```bash
python batch_generate.py /media --format srt   # SubRip (most compatible)
python batch_generate.py /media --format vtt   # WebVTT
python batch_generate.py /media --format txt   # Plain text
python batch_generate.py /media --format json  # JSON format
```

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
