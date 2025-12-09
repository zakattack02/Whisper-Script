# Jellyfin Whisper Subtitles Plugin

Automatically generate subtitles for your Jellyfin media library using OpenAI's Whisper AI.

**Repository:** [zakattack02/Whisper-Script](https://github.com/zakattack02/Whisper-Script) | **Branch:** `feature/jellyfin-plugin`

---

## Project Status

**✅ CORE IMPLEMENTATION COMPLETE** - All services implemented and refactored. Plugin successfully migrated from broken Whisper.NET library to working whisper.cpp CLI approach.

**Version:** 0.0.0.36 (Latest Release)  
**Target:** Jellyfin v10.11.2+  
**Framework:** .NET 9.0  
**License:** GPLv3

---

## ✅ What's Been Completed

### Architecture & Services
- ✅ .NET 9.0 project structure (upgraded from .NET 8.0)
- ✅ Jellyfin packages (10.11.2 compatibility)
- ✅ **Plugin configuration system** with all settings
- ✅ **Complete web UI** for configuration (`configPage.html`)
- ✅ **IWhisperService interface** - Clean service abstraction
- ✅ **WhisperService.cs** - Core subtitle generation (refactored to whisper.cpp CLI)
- ✅ **ISubtitleDetectionService interface**
- ✅ **SubtitleDetectionService** - Subtitle file detection and management
- ✅ Model download & caching system (direct Hugging Face downloads)
- ✅ **SRT subtitle file generation**
- ✅ HttpClient integration for downloads
- ✅ Successful compilation (0 errors, 0 warnings)
- ✅ GPLv3 License

### Critical Bug Fixes
- ✅ Fixed version display bug (now shows 0.0.0.36 correctly)
- ✅ Resolved native library loading failure (11,415+ errors)
- ✅ Migrated from Whisper.NET library to whisper.cpp CLI approach
- ✅ Removed problematic NuGet dependencies

### Build & Deployment
- ✅ Automated make-release.sh script
- ✅ Interactive version selection (patch/minor/major/manual/current)
- ✅ Changelog input via Ctrl+D
- ✅ 7-step release workflow (build → package → manifest → GitHub)
- ✅ GitHub integration for releases
- ✅ Manifest.json automatic updates

### Documentation
- ✅ WHISPER_CPP_SETUP.md - Complete setup guide
- ✅ DOCKER_SETUP.md - Docker deployment examples
- ✅ MIGRATION_GUIDE.md - Technical migration details
- ✅ RELEASE_NOTES.md - v0.0.0.36 information
- ✅ IMPLEMENTATION_SUMMARY.md - Full technical documentation

---

## Project Structure

```
Jellyfin.Plugin.WhisperSubtitles/
├── Configuration/
│   ├── PluginConfiguration.cs      ✅ Complete
│   ├── configPage.html             ✅ Complete (with improved error handling)
│   └── PluginConfigurationPage.cs  ✅ Complete
├── Services/
│   ├── IWhisperService.cs          ✅ Complete
│   ├── WhisperService.cs           ✅ Complete (refactored)
│   ├── ISubtitleDetectionService.cs ✅ Complete
│   └── SubtitleDetectionService.cs  ✅ Complete
├── Controllers/
│   └── WhisperSubtitlesController.cs ✅ Complete (updated)
├── Tasks/
│   ├── WhisperSubtitleTask.cs      ⏳ TODO
│   └── WhisperPostScanTask.cs      ⏳ TODO
└── Plugin.cs                       ✅ Complete
```

---

## 🚀 Key Features

### Currently Implemented
- ✅ **CLI-based Whisper Integration** - Uses whisper.cpp binary instead of problematic library
- ✅ **Automatic Model Download** - Direct Hugging Face downloads with progress tracking
- ✅ **Model Caching** - Models stored in `~/.cache/whisper/` for reuse
- ✅ **Configurable Models** - Choose from tiny, base, small, medium, turbo, large
- ✅ **Language Support** - Transcribe or translate to target language
- ✅ **Translation Support** - Auto-translate audio to English or other languages
- ✅ **SRT Generation** - High-quality SRT subtitle format output
- ✅ **Subtitle Detection** - Smart detection of existing subtitles
- ✅ **AI Identifier** - Mark AI-generated subtitles (e.g., video.en.whisper.srt)
- ✅ **Word Timestamps** - Optional word-level timing for karaoke-style subtitles
- ✅ **Multiple Formats** - Support for srt, vtt, ass, ssa, sub, idx formats
- ✅ **Long Filename Handling** - Proper 255-character limit truncation

### Coming Soon
- ⏳ **WhisperSubtitleTask** - Scheduled task implementation
- ⏳ **WhisperPostScanTask** - Library scan hook
- ⏳ **Batch Processing** - Queue system for multiple videos
- ⏳ **Progress Reporting** - Real-time progress in UI
- ⏳ **Library Selection** - Choose which libraries to process

---

## 📊 Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| **Whisper Model** | Select | small | Model size (tiny/base/small/medium/large/turbo) |
| **Target Language** | Text | en | Language code (en, es, fr, de, ja, etc.) |
| **Translate to English** | Checkbox | ✓ | Translate audio to English |
| **AI Identifier** | Text | whisper | String to add to subtitle filenames |
| **Word Timestamps** | Checkbox | ☐ | Enable word-level timing |
| **Process on Library Scan** | Checkbox | ☐ | Auto-generate on library updates |
| **Skip Existing** | Checkbox | ✓ | Don't process files with subtitles |
| **Regenerate AI** | Checkbox | ☐ | Re-process AI-generated subtitles |

---

## Supported Models

| Model | Parameters | Speed | VRAM | Download Size |
|-------|-----------|-------|------|---|
| **tiny** | 39M | ~10x | ~1 GB | ~75 MB |
| **base** | 74M | ~7x | ~1 GB | ~140 MB |
| **small** | 244M | ~4x | ~2 GB | ~460 MB |
| **medium** | 769M | ~2x | ~5 GB | ~1.5 GB |
| **large** | 1550M | 1x | ~10 GB | ~3 GB |
| **turbo** | 809M | ~8x | ~6 GB | ~1.6 GB |

**Recommended:** Small model for balanced quality/speed/resource usage.

---

## 🔧 Installation & Setup

### Prerequisites
- Jellyfin Server v10.11.2 or later
- whisper.cpp binary installed

### Installing whisper.cpp

#### Docker (Recommended)
```dockerfile
RUN git clone https://github.com/ggerganov/whisper.cpp.git /tmp/whisper.cpp && \
    cd /tmp/whisper.cpp && make && cp main /usr/local/bin/whisper
```

#### Manual (Linux/macOS)
```bash
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
make
sudo cp main /usr/local/bin/whisper
```

#### With GPU Support (CUDA)
```bash
cd whisper.cpp
make WHISPER_CUDA=1
sudo cp main /usr/local/bin/whisper
```

See [WHISPER_CPP_SETUP.md](./WHISPER_CPP_SETUP.md) and [DOCKER_SETUP.md](./DOCKER_SETUP.md) for detailed instructions.

### Installing the Plugin

1. **Build the plugin:**
   ```bash
   cd Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles
   dotnet publish --configuration Release
   ```

2. **Copy to Jellyfin plugins directory:**
   ```bash
   mkdir -p ~/.config/jellyfin/plugins
   cp -r bin/Release/net9.0/publish/* ~/.config/jellyfin/plugins/Whisper\ Subtitles/
   ```

3. **Restart Jellyfin**

4. **Configure via Dashboard:**
   - Dashboard → Plugins → Whisper Subtitles
   - Select model, language, and options
   - Click "Download Selected Model"
   - Save configuration

---

##  Building

### Build Solution
```bash
dotnet build Jellyfin.Plugin.WhisperSubtitles.sln
```

### Build with Release Configuration
```bash
dotnet build --configuration Release
```

### Publish Plugin
```bash
dotnet publish --configuration Release
```

**Build Status:** ✅ Succeeds with 0 errors, 0 warnings

---

##  Release Process

Automated releases via `make-release.sh`:

```bash
./make-release.sh
```

**Options:**
1. **Patch** - Increment patch version (0.0.0.36 → 0.0.0.37)
2. **Minor** - Increment minor version (0.0.0.36 → 0.1.0.0)
3. **Major** - Increment major version (0.0.0.36 → 1.0.0.0)
4. **Manual** - Enter custom version
5. **Current** - Use current version without changes

**Features:**
- Interactive version selection
- Ctrl+D to input multi-line changelog
- Automatic build and packaging
- Manifest.json updates
- GitHub release creation
- Automatic artifact upload

See [make-release.sh](./make-release.sh) for full automation details.

---

##  Architecture & Technical Details

### Whisper.NET → whisper.cpp Migration

**Problem:** Whisper.NET library required native C++ runtime libraries not available in containers.
- Error: "Failed to load native whisper library"
- Impact: 11,415+ subtitle generation errors

**Solution:** Complete migration to whisper.cpp CLI-based execution.
- Single portable binary
- No dependency issues
- Same or better performance
- Easy GPU acceleration

See [IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md) for full technical details.

### Service Architecture

```csharp
// IWhisperService - Core API
Task<bool> GenerateSubtitleAsync(string videoPath, string outputPath, ...)
Task DownloadModelAsync(string modelName, CancellationToken cancellationToken)
Task TestAsync(CancellationToken cancellationToken)

// ISubtitleDetectionService - Subtitle management
bool HasSubtitlesByLanguage(string videoPath, string language)
bool HasAISubtitles(string videoPath, string aiIdentifier)
string[] GetAllSubtitleFiles(string videoPath)
string GenerateSubtitlePath(string videoPath, string language, string? aiIdentifier)
```

### Process Execution

WhisperService uses `System.Diagnostics.Process` to execute whisper CLI:

```bash
whisper -m /path/to/model.bin -f /path/to/video.mp4 -o /output/dir [options]
```

Arguments are automatically built based on configuration:
- Model selection
- Output format (SRT)
- Language specification
- Translation flags
- Word-level timestamps
- GPU acceleration (if configured)

---

##  Troubleshooting

### whisper Binary Not Found
```bash
# Check if whisper is in PATH
which whisper

# Install if missing
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp && make && sudo cp main /usr/local/bin/whisper
```

### Model Download Issues
1. Check disk space in `~/.cache/whisper/`
2. Verify network connectivity
3. Check Jellyfin logs for specific errors
4. Try downloading smaller model first (tiny)

### Subtitle Generation Hangs
1. Verify video file is valid
2. Check if whisper process is running: `ps aux | grep whisper`
3. Check system resources (CPU/RAM/Disk)
4. Review Jellyfin logs for error messages

### GPU Not Being Used
1. Verify whisper compiled with GPU support: `whisper --help | grep gpu`
2. Check CUDA/HIP installation
3. Ensure sufficient VRAM available
4. GPU activation is automatic when available

See [WHISPER_CPP_SETUP.md](./WHISPER_CPP_SETUP.md) for complete troubleshooting guide.

---

##  Documentation

- **[WHISPER_CPP_SETUP.md](./WHISPER_CPP_SETUP.md)** - Complete whisper.cpp installation and configuration
- **[DOCKER_SETUP.md](./DOCKER_SETUP.md)** - Docker deployment examples and compose files
- **[MIGRATION_GUIDE.md](./MIGRATION_GUIDE.md)** - Technical details of Whisper.NET → whisper.cpp migration
- **[RELEASE_NOTES.md](./RELEASE_NOTES.md)** - v0.0.0.36 release information
- **[IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md)** - Full implementation details

---

## 🔗 References

- [whisper.cpp GitHub](https://github.com/ggerganov/whisper.cpp) - Official whisper.cpp project
- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Jellyfin Plugin Template](https://github.com/jellyfin/jellyfin-plugin-template)
- [OpenAI Whisper](https://github.com/openai/whisper)

---

##  Licensing

This project is licensed under **GPLv3**.

### Important Notice
Due to Jellyfin's plugin architecture and GPLv3 licensing of Jellyfin packages:
- The compiled plugin binary will be licensed under GPLv3
- Source code must be publicly available
- Proprietary or "hidden" plugins are not permitted
- Any modifications must also be open-source

See [LICENSE](./LICENSE) for full GPLv3 text.

---

## 🤝 Contributing

Contributions are welcome! This is an open-source project under GPLv3.

Areas for contribution:
- Task implementations (WhisperSubtitleTask, WhisperPostScanTask)
- GPU acceleration optimization
- Additional language support
- Improved error handling
- Documentation improvements
- Bug reports and fixes

---


