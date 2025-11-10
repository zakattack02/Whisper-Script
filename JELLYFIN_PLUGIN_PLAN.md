# Jellyfin Whisper Subtitle Plugin - Development Plan

## Overview
Convert the Python Whisper subtitle generator into a Jellyfin plugin written in C# that integrates directly with Jellyfin's library system.

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
- [ ] Scheduled task to scan library for videos without subtitles
- [ ] Manual trigger from plugin configuration page
- [ ] Configurable settings (model size, language, etc.)
- [ ] Progress tracking and logging
- [ ] Queue system for batch processing

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

### Phase 1: Setup (Option 1 - Hybrid)
1. [ ] Clone jellyfin-plugin-template
2. [ ] Rename project to `Jellyfin.Plugin.WhisperSubtitles`
3. [ ] Set up basic plugin structure
4. [ ] Create configuration class
5. [ ] Build configuration web UI

### Phase 2: Core Integration
1. [ ] Implement `IScheduledTask` for manual generation
2. [ ] Add Python script execution logic
3. [ ] Implement progress reporting
4. [ ] Add error handling and logging
5. [ ] Test with small library

### Phase 3: Advanced Features
1. [ ] Implement `ILibraryPostScanTask` for auto-generation
2. [ ] Add library selection in config
3. [ ] Queue system for batch processing
4. [ ] Notification support (on completion/errors)
5. [ ] Statistics and reporting

### Phase 4: Polish
1. [ ] Comprehensive error messages
2. [ ] User documentation
3. [ ] Installation guide
4. [ ] Create plugin repository manifest
5. [ ] Package for distribution

## File Structure

```
Jellyfin.Plugin.WhisperSubtitles/
├── Configuration/
│   ├── PluginConfiguration.cs
│   └── configPage.html
├── Tasks/
│   ├── WhisperSubtitleTask.cs
│   └── WhisperPostScanTask.cs
├── Services/
│   ├── IWhisperService.cs
│   ├── WhisperService.cs (Python bridge)
│   └── SubtitleDetectionService.cs
├── Plugin.cs
├── Scripts/
│   └── batch_generate.py (embedded as resource)
└── README.md
```

## Alternative: Pure C# with Whisper.NET

If going with Option 2, key changes:

```csharp
using Whisper.net;

public class WhisperService
{
    private WhisperProcessor _processor;
    
    public async Task GenerateSubtitle(string videoPath, string model)
    {
        using var processor = WhisperFactory.FromPath(model);
        using var fileStream = File.OpenRead(videoPath);
        
        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            // Write SRT format
        }
    }
}
```

**Required NuGet Packages:**
- Whisper.net
- Whisper.net.Runtime (for native binaries)

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

1. **Decision Point**: Choose Option 1 (Hybrid) or Option 2 (Pure C#)
   - **Recommendation**: Start with Option 1 for faster MVP
   - Can migrate to Option 2 later if needed

2. **Setup Development Environment**:
   ```bash
   # Install .NET SDK 8.0
   sudo pacman -S dotnet-sdk
   
   # Clone and customize template
   cd /home/kanucks/Documents/Whisper
   dotnet new -i /path/to/jellyfin-plugin-template
   dotnet new Jellyfin-plugin -name WhisperSubtitles
   ```

3. **Initial Implementation**:
   - Create basic plugin structure
   - Implement configuration
   - Add scheduled task
   - Test with your existing Python script

## Resources

- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Plugin Template](https://github.com/jellyfin/jellyfin-plugin-template)
- [Whisper.NET](https://github.com/sandrohanea/whisper.net)
- [Jellyfin API Docs](https://api.jellyfin.org/)

## Questions to Answer

1. Do you want to keep Python script or go pure C#?
2. Should it auto-generate on library scan or only manual/scheduled?
3. Do you want to package Python environment with plugin?
4. Target Jellyfin version? (10.8.x or 10.9.x)
