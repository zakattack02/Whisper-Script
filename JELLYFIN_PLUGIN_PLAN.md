# Jellyfin Whisper Subtitle Plugin - Development Plan

## Overview
Convert the Python Whisper subtitle generator into a Jellyfin plugin written in C# that integrates directly with Jellyfin's library system.

**DECISION MADE:** Option 2 - Pure C# Implementation with Whisper.NET

## Current Status

### ✅ Completed
- [x] .NET 8.0 project structure
- [x] Jellyfin packages (10.8.13 for compatibility)
- [x] Whisper.NET integration (v1.8.1)
- [x] Plugin configuration system
- [x] Configuration web UI
- [x] IWhisperService interface
- [x] WhisperService implementation with:
  - [x] Model download & caching
  - [x] SRT subtitle generation
  - [x] Translation support
  - [x] Word timestamps support
  - [x] HttpClient integration
  - [x] Proper disposal pattern
  - [x] Comprehensive logging
- [x] ISubtitleDetectionService interface
- [x] SubtitleDetectionService implementation with:
  - [x] Check for existing subtitle files
  - [x] Detect AI-generated subtitles by identifier
  - [x] Get all subtitle files for a video
  - [x] Generate subtitle paths with AI identifier
  - [x] Handle long filenames (255 char limit)
  - [x] Support multiple formats (srt, vtt, ass, ssa, sub, idx)
- [x] GPLv3 License
- [x] Successful compilation

### ⏳ In Progress
- [ ] WhisperSubtitleTask (IScheduledTask)
- [ ] WhisperPostScanTask (ILibraryPostScanTask)
- [ ] Dependency injection setup
- [ ] Testing with real Jellyfin instance

## Architecture Options

### Option 1: Hybrid Approach (Recommended for MVP)
**C# Plugin → Python Script Bridge**

**Pros:**
- Reuse existing Python code
- Faster initial development
- Easier to maintain Python Whisper logic separately

**Cons:**
- Requires Python runtime on Jellyfin server
- External dependency management
- Less integrated experience

**Implementation:**
```csharp
// Plugin calls Python script via Process
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "python3",
        Arguments = $"{pythonScriptPath} {mediaFolder} --model {model}",
        UseShellExecute = false,
        RedirectStandardOutput = true
    }
};
```

### Option 2: Pure C# Implementation
**Full C# Plugin with Whisper.NET**

**Pros:**
- Fully integrated with Jellyfin
- No external dependencies
- Better performance
- Native .NET experience

**Cons:**
- More development time
- Need to port Python logic to C#
- Requires Whisper.NET library

**Libraries:**
- [Whisper.NET](https://github.com/sandrohanea/whisper.net) - C# bindings for OpenAI Whisper

## Plugin Features

### Core Functionality
- [x] Configurable settings (model size, language, translation, etc.)
- [x] Whisper model download and caching
- [x] SRT subtitle file generation
- [ ] Scheduled task to scan library for videos without subtitles
- [ ] Manual trigger from plugin configuration page
- [ ] Progress tracking and logging
- [ ] Queue system for batch processing
- [ ] Subtitle detection (skip existing, regenerate AI)

### Jellyfin Interfaces to Implement

#### 1. `IScheduledTask`
Allows scheduled subtitle generation:
```csharp
public class WhisperSubtitleTask : IScheduledTask
{
    public string Name => "Generate Whisper Subtitles";
    public string Key => "WhisperSubtitleGeneration";
    public string Description => "Generates AI subtitles using Whisper";
    
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Scan libraries and generate subtitles
    }
}
```

#### 2. `ILibraryPostScanTask`
Auto-generate subtitles after library scan:
```csharp
public class WhisperPostScanTask : ILibraryPostScanTask
{
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Generate subtitles for newly added media
    }
}
}
```

#### 3. `IPluginConfigurationPage`
Web UI for configuration (included in template)

## Configuration Settings

```csharp
public class PluginConfiguration : BasePluginConfiguration
{
    public string WhisperModel { get; set; } = "small";
    public string TargetLanguage { get; set; } = "en";
    public bool TranslateToEnglish { get; set; } = true;
    public string AIIdentifier { get; set; } = "whisper";
    public bool WordTimestamps { get; set; } = false;
    public bool ProcessOnLibraryScan { get; set; } = false;
    public string[] LibrariesToProcess { get; set; } = Array.Empty<string>();
    public bool SkipExisting { get; set; } = true;
    public bool RegenerateAI { get; set; } = false;
    
    // Option 1 specific
    public string PythonExecutablePath { get; set; } = "/usr/bin/python3";
    public string ScriptPath { get; set; } = "";
}
```

## Development Steps

### Phase 1: Setup ✅ COMPLETED
- [x] Choose Option 2 (Pure C# with Whisper.NET)
- [x] Set up basic plugin structure
- [x] Create configuration class
- [x] Build configuration web UI
- [x] Implement IWhisperService interface
- [x] Implement WhisperService with full Whisper.NET integration
- [x] Add model download & caching
- [x] Compile successfully

### Phase 2: Core Integration ⏳ IN PROGRESS
- [x] Create SubtitleDetectionService
  - [x] Check for existing subtitle files
  - [x] Detect AI-generated subtitles by identifier
  - [x] Handle long filenames (255 char limit)
  - [x] Support multiple subtitle formats
  - [x] Generate subtitle output paths
- [ ] Implement `IScheduledTask` for manual generation
- [ ] Add Jellyfin library scanning logic
- [ ] Implement progress reporting
- [ ] Add error handling and logging
- [ ] Test with small library

### Phase 3: Advanced Features
- [ ] Implement `ILibraryPostScanTask` for auto-generation
- [ ] Add library selection in config
- [ ] Queue system for batch processing
- [ ] Notification support (on completion/errors)
- [ ] Statistics and reporting
- [ ] GPU acceleration support

### Phase 4: Polish
- [ ] Comprehensive error messages
- [ ] User documentation
- [ ] Installation guide
- [ ] Create plugin repository manifest
- [ ] Package for distribution

## File Structure

```
Jellyfin.Plugin.WhisperSubtitles/
├── Configuration/
│   ├── PluginConfiguration.cs        ✅ Complete
│   └── configPage.html               ✅ Complete
├── Tasks/
│   ├── WhisperSubtitleTask.cs        ⏳ TODO
│   └── WhisperPostScanTask.cs        ⏳ TODO
├── Services/
│   ├── IWhisperService.cs            ✅ Complete
│   ├── WhisperService.cs             ✅ Complete
│   ├── ISubtitleDetectionService.cs  ✅ Complete
│   └── SubtitleDetectionService.cs   ✅ Complete
├── Plugin.cs                         ✅ Complete
└── README.md                         ✅ Updated
```

## Alternative: Pure C# with Whisper.NET ✅ IMPLEMENTED

We chose Option 2 and implemented the full C# solution:

```csharp
using Whisper.net;
using Whisper.net.Ggml;

public class WhisperService : IWhisperService
{
    private readonly ILogger<WhisperService> _logger;
    private readonly HttpClient _httpClient;
    
    public async Task<bool> GenerateSubtitleAsync(
        string videoPath, string outputPath, string model,
        string language, bool translate, bool wordTimestamps,
        CancellationToken cancellationToken)
    {
        // Download model if needed
        if (!IsModelAvailable(model))
        {
            await DownloadModelAsync(model, cancellationToken);
        }
        
        // Create Whisper processor
        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();
        
        // Process video and generate subtitles
        await using var fileStream = File.OpenRead(videoPath);
        var segments = new List<SegmentData>();
        
        await foreach (var segment in processor.ProcessAsync(fileStream, cancellationToken))
        {
            segments.Add(segment);
        }
        
        // Write SRT file
        await WriteSrtFileAsync(outputPath, segments, cancellationToken);
        return true;
    }
}
```

**Implemented Features:**
- ✅ Model download via WhisperGgmlDownloader
- ✅ Model caching in `~/.cache/whisper/`
- ✅ All models supported (tiny, base, small, medium, large-v1/v2/v3, turbo)
- ✅ SRT subtitle format with proper timestamps
- ✅ Language detection and translation
- ✅ Word-level timestamps (configurable)
- ✅ HttpClient for downloads
- ✅ Proper resource disposal
- ✅ Subtitle detection and management
- ✅ AI identifier support in filenames
- ✅ Multiple subtitle format support
- ✅ Long filename handling

**Required NuGet Packages:** ✅ Installed
- Whisper.net (v1.8.1)
- Whisper.net.Runtime (v1.8.1) - for native binaries
- Jellyfin.Model (v10.8.13)
- Jellyfin.Controller (v10.8.13)

## Testing Strategy

1. **Unit Tests**: Configuration, subtitle detection logic
2. **Integration Tests**: Jellyfin library interaction
3. **Manual Testing**: Real Jellyfin instance with test media

## Distribution

### Plugin Repository JSON
```json
{
  "guid": "YOUR-PLUGIN-GUID-HERE",
  "name": "Whisper Subtitles",
  "description": "Automatically generate subtitles using OpenAI Whisper",
  "overview": "Generate AI-powered subtitles for your media library",
  "owner": "zakattack02",
  "category": "Metadata",
  "versions": [
    {
      "version": "1.0.0",
      "changelog": "Initial release",
      "targetAbi": "10.9.0.0",
      "sourceUrl": "https://github.com/zakattack02/jellyfin-plugin-whisper/releases/download/v1.0.0/whisper-subtitles.zip",
      "checksum": "..."
    }
  ]
}
```

## Next Steps

1. **✅ Decision Point: Choose Option 1 (Hybrid) or Option 2 (Pure C#)**
   - **DECISION: Option 2 (Pure C#) - IMPLEMENTED**
   - Full Whisper.NET integration complete
   - No Python dependencies required

2. **✅ Setup Development Environment**: COMPLETE
   ```bash
   # .NET SDK 8.0 installed
   # Project structure created
   # All dependencies installed
   # Compiles successfully
   ```

3. **⏳ Current Implementation Focus**:
   - ✅ ~~Create SubtitleDetectionService for checking existing subtitles~~ **COMPLETED**
   - Implement WhisperSubtitleTask (IScheduledTask)
   - Implement WhisperPostScanTask (ILibraryPostScanTask)
   - Set up dependency injection in Plugin.cs
   - Test with actual Jellyfin server

4. **📋 Immediate Next Tasks**:
   - WhisperSubtitleTask.cs - Scheduled task implementation
   - WhisperPostScanTask.cs - Library scan hook
   - Register services in Plugin.cs
   - Package plugin for testing

## Resources

- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Plugin Template](https://github.com/jellyfin/jellyfin-plugin-template)
- [Whisper.NET](https://github.com/sandrohanea/whisper.net)
- [Jellyfin API Docs](https://api.jellyfin.org/)

## Questions to Answer

1. ✅ ~~Do you want to keep Python script or go pure C#?~~ **ANSWERED: Pure C# with Whisper.NET**
2. ⏳ Should it auto-generate on library scan or only manual/scheduled? **TODO: Implement both options**
3. ✅ ~~Do you want to package Python environment with plugin?~~ **N/A - No Python needed**
4. ✅ ~~Target Jellyfin version? (10.8.x or 10.9.x)~~ **ANSWERED: 10.8.x (.NET 8.0)**

## Performance Considerations

- Model caching prevents re-downloads
- Consider GPU acceleration for faster processing
- Queue system to prevent overload
- Configurable batch size for library scans
- Progress reporting for user feedback
