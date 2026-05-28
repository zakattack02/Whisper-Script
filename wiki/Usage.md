# Usage

## Running the Task

1. Dashboard → Scheduled Tasks → **Generate Whisper Subtitles**
2. Click the **play** (▶) button to run immediately
3. Or configure a trigger (e.g., daily at 2 AM)

The task processes all videos in the selected libraries (or all libraries, if none are configured).

## What Happens During Processing

For each video:

1. **Skip check** — The plugin checks for existing subtitles. If `Skip Existing` is on and subtitles exist (non-AI), the video is skipped.
2. **Audio extraction** — FFmpeg extracts audio as 16kHz mono WAV.
3. **Duration check** — If audio > 30 minutes, it's split into chunks.
4. **Transcription** — Each chunk (or the full audio) is processed by whisper.cpp.
5. **SRT merging** — Chunks are merged into a single SRT with sequential numbering.
6. **File tagging** — The output filename includes the AI identifier tag.

### File Naming

```
Original:    /Media/Movies/My Movie (2024).mkv
Subtitle:    /Media/Movies/My Movie (2024).en.whisper.srt
                                         ↑  ↑  ↑
                                      lang ID  tag
```

## Scheduled Triggers

The task supports Jellyfin's scheduling system. Common setups:

- **Daily** — run every night at 3 AM
- **Weekly** — run every Sunday
- **On library scan** — use "Process on Library Scan" in config instead

## Monitoring Progress

The scheduled task UI shows percentage progress. With chunked videos, progress updates per-chunk:

- 22 videos, 1 video chunked into 6 parts = 27 "ticks"
- Progress: `0% → 3.7% → 7.4% → ... → 100%`

## Logs

Check the Jellyfin logs for detailed per-step logging:

```
[INF] Whisper task starting. Model=Small, Language="en", ...
[INF] Generating: /Media/Movies/My Movie (2024).mkv
[INF] Extracting audio ...
[INF] Audio extracted (123456789 bytes)
[INF] Audio split into 4 chunk(s)
[INF] Using CUDA binary at /cache/whisper-cpp/whisper-whisper-cli-cuda
[INF] whisper: "main: processing ... (1800.0 sec), 6 threads ..."
[INF] whisper: "main: processing ... (1800.0 sec), 6 threads ..."
[INF] Subtitles written: My Movie (2024).en.whisper.srt (12345 bytes)
[INF] Task complete. Generated=1, Skipped=0, Errors=0
```

## Interpreting Results

Check for the `.en.whisper.srt` (or `.{lang}.{tag}.srt`) file next to the video in Jellyfin. If subtitles don't appear:

1. Scan the media library manually
2. Check if the subtitle file exists on disk
3. Verify the language tag matches what Jellyfin expects

## Post-Scan Processing

If "Process on Library Scan" is enabled, the plugin automatically generates subtitles for new media items when:
- A library scan completes
- New files are detected by the file system watcher

Use `FoldersToExclude` to prevent processing in certain directories.
