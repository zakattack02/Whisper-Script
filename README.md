# Jellyfin Whisper Subtitles Plugin

Automatically generate subtitles for your Jellyfin media library using OpenAI's Whisper AI.

## Project Status

🚧 **IN DEVELOPMENT** - Core services implemented, tasks and integration in progress.

## What's Been Completed

✅ .NET 8.0 project structure  
✅ Jellyfin packages installed (10.8.x for .NET 8 compatibility)  
✅ Whisper.NET library integrated (v1.8.1)  
✅ Plugin configuration system with all settings  
✅ Complete web UI for configuration  
✅ **WhisperService** - Core subtitle generation with Whisper.NET  
✅ **IWhisperService** interface - Clean service abstraction  
✅ **SubtitleDetectionService** - Subtitle file detection and management  
✅ **ISubtitleDetectionService** interface  
✅ Model download & caching system  
✅ SRT subtitle file generation  
✅ GPLv3 License  
✅ HttpClient integration for model downloads  
✅ Successful compilation

## Next Steps

### Immediate Tasks
1. ✅ ~~Create `WhisperService.cs`~~ - **COMPLETED**
2. ✅ ~~Create `SubtitleDetectionService.cs`~~ - **COMPLETED**
3. Create `WhisperSubtitleTask.cs` - Scheduled task implementation
4. Create `WhisperPostScanTask.cs` - Library scan hook
5. Register services with dependency injection
6. Test with actual Jellyfin server
7. Package and deploy plugin

### Architecture

```
Jellyfin.Plugin.WhisperSubtitles/
├── Configuration/
│   ├── PluginConfiguration.cs     ✅ Created
│   └── configPage.html             ✅ Created
├── Services/
│   ├── IWhisperService.cs          ✅ Created
│   ├── WhisperService.cs           ✅ Created
│   ├── ISubtitleDetectionService.cs ✅ Created
│   └── SubtitleDetectionService.cs  ✅ Created
├── Tasks/
│   ├── WhisperSubtitleTask.cs      ⏳ TODO
│   └── WhisperPostScanTask.cs      ⏳ TODO  
└── Plugin.cs                       ✅ Created
```

## Features (Planned)

- **Scheduled Task**: Manual subtitle generation via Jellyfin's scheduled tasks
- **Post-Library Scan**: Automatic generation after library updates
- **Configurable Models**: Choose from tiny, base, small, medium, turbo, large
- **Translation Support**: Translate any language to English or transcribe in original language
- **AI Identifier**: Mark AI-generated subtitles (e.g., video.en.whisper.srt)
- **Word Timestamps**: Optional word-level timing for karaoke-style subtitles
- **Smart Detection**: Skip files that already have subtitles
- **Library Selection**: Choose which libraries to process

## Configuration Options

- **Whisper Model**: Model size (tiny to large, plus turbo)
- **Target Language**: Language code for output (en, es, fr, etc.)
- **Translate to English**: Convert any language to English
- **AI Identifier**: String to add to filenames
- **Word Timestamps**: Enable word-level timing
- **Process on Library Scan**: Auto-generate on library updates
- **Skip Existing**: Don't process files with language subtitles
- **Regenerate AI**: Re-process AI-generated subtitles

## Building

```bash
# Build the solution
dotnet build Jellyfin.Plugin.WhisperSubtitles.sln

# Or build just the project
cd Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles
dotnet build
```

**Current Build Status:** ✅ Compiles successfully

## Installation (Once Complete)

1. Build the plugin
2. Copy the DLL to Jellyfin's plugin directory
3. Restart Jellyfin
4. Configure via Dashboard → Plugins → Whisper Subtitles

## Development Notes

### Why .NET 8?
- Jellyfin 10.8.x uses .NET 8
- Jellyfin 10.9+ moved to .NET 9
- Targeting 10.8.x for broader compatibility

### Whisper.NET
- C# bindings for OpenAI Whisper (v1.8.1)
- Native performance with GPU support potential
- Cross-platform (Windows, Linux, macOS)
- Model download and caching built-in
- Supports all Whisper models: tiny, base, small, medium, large-v1/v2/v3, turbo

## Current Implementation Details

### WhisperService Features
- ✅ Automatic model download from Hugging Face
- ✅ Model caching in `~/.cache/whisper/`
- ✅ SRT subtitle file generation
- ✅ Configurable language detection
- ✅ Translation support
- ✅ Word-level timestamps (configurable)
- ✅ Proper resource disposal (HttpClient, streams)
- ✅ Comprehensive logging
- ✅ Cancellation token support

### SubtitleDetectionService Features
- ✅ Check for existing subtitle files by language
- ✅ Detect AI-generated subtitles by identifier
- ✅ Get all subtitle files for a video
- ✅ Generate subtitle output paths with AI identifier
- ✅ Handle long filenames (255 char limit with truncation)
- ✅ Support multiple subtitle formats (srt, vtt, ass, ssa, sub, idx)
- ✅ Pattern matching for forced/SDH subtitles

### Supported Models
| Model | Parameters | Relative Speed | VRAM |
|-------|-----------|----------------|------|
| tiny | 39M | ~10x | ~1 GB |
| base | 74M | ~7x | ~1 GB |
| small | 244M | ~4x | ~2 GB |
| medium | 769M | ~2x | ~5 GB |
| large-v3 | 1550M | 1x | ~10 GB |
| turbo | 809M | ~8x | ~6 GB |

## References

- [Whisper.NET GitHub](https://github.com/sandrohanea/whisper.net)
- [whisper.cpp Github](https://github.com/ggml-org/whisper.cpp)
- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)


## Licensing

Licensing is a complex topic. This repository features a GPLv3 license template that can be used to provide a good default license for your plugin. You may alter this if you like, but if you do a permissive license must be chosen.

Due to how plugins in Jellyfin work, when your plugin is compiled into a binary, it will link against the various Jellyfin binary NuGet packages. These packages are licensed under the GPLv3. Thus, due to the nature and restrictions of the GPL, the binary plugin you get will also be licensed under the GPLv3.

If you accept the default GPLv3 license from this template, all will be good. However if you choose a different license, please keep this fact in mind, as it might not always be obvious that an, e.g. MIT-licensed plugin would become GPLv3 when compiled.

Please note that this also means making "proprietary", source-unavailable, or otherwise "hidden" plugins for public consumption is not permitted. To build a Jellyfin plugin for distribution to others, it must be under the GPLv3 or a permissive open-source license that can be linked against the GPLv3.
