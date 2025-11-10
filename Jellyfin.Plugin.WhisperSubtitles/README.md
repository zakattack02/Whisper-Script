# Jellyfin Whisper Subtitles Plugin

Automatically generate subtitles for your Jellyfin media library using OpenAI's Whisper AI.

## Project Status

🚧 **IN DEVELOPMENT** - This is the initial setup of the Jellyfin plugin using pure C# with Whisper.NET.

## What's Been Set Up

✅ .NET 8.0 project structure  
✅ Jellyfin packages installed (10.8.x for .NET 8 compatibility)  
✅ Whisper.NET library integrated  
✅ Plugin configuration system  
✅ Basic web UI for configuration  
✅ Project on `feature/jellyfin-plugin` branch  

## Next Steps

### Immediate Tasks
1. Create `WhisperService.cs` - Core service to handle Whisper transcription
2. Create `WhisperSubtitleTask.cs` - Scheduled task implementation
3. Implement subtitle detection logic
4. Add library scanning support
5. Test compilation and basic functionality

### Architecture

```
Jellyfin.Plugin.WhisperSubtitles/
├── Configuration/
│   ├── PluginConfiguration.cs     ✅ Created
│   └── configPage.html             ✅ Created
├── Services/
│   ├── IWhisperService.cs          ⏳ TODO
│   ├── WhisperService.cs           ⏳ TODO
│   └── SubtitleDetectionService.cs ⏳ TODO
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
- **Skip Existing**: Don't process files with subtitles
- **Regenerate AI**: Re-process AI-generated subtitles

## Building

```bash
cd Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles
dotnet build
```

## Installation (Once Complete)

1. Build the plugin
2. Copy the DLL to Jellyfin's plugin directory
3. Restart Jellyfin
4. Configure via Dashboard → Plugins → Whisper Subtitles

## Development Notes

### Why .NET 8?
- Jellyfin 10.8.x uses .NET 8
- Jellyfin 10.9+ moved to .NET 9
- We're targeting 10.8.x for broader compatibility

### Whisper.NET
- C# bindings for OpenAI Whisper
- Native performance with GPU support
- Cross-platform (Windows, Linux, macOS)

## References

- [Whisper.NET GitHub](https://github.com/sandrohanea/whisper.net)
- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Original Python Script](../../batch_generate.py)

## License

GPL-3.0 (required by Jellyfin plugin system)
