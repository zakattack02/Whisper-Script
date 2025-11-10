#!/usr/bin/env python3
"""
Whisper Subtitle Generator for Jellyfin
Automatically generate subtitles for your media library using OpenAI's Whisper.

Usage:
    python batch_generate.py                    # Use configured folders below
    python batch_generate.py /path/to/media    # Process specific folder
    python batch_generate.py --help            # Show all options
"""

import os
import sys
import argparse
from pathlib import Path
import whisper
from tqdm import tqdm

# ============================================================================
# DEFAULT CONFIGURATION
# Edit these values or override with command-line arguments
# ============================================================================

DEFAULT_MEDIA_FOLDERS = [
    # Add your media folder paths here
    # Examples:
    # "/mnt/jellyfin/anime/",
    # "/mnt/jellyfin/movies/",
    # "/path/to/tv-shows/",
]

DEFAULT_MODEL = "medium"                     # tiny, base, small, medium, large, turbo
DEFAULT_FORMAT = "srt"                       # srt, vtt, txt, json
DEFAULT_LANGUAGE = "en"                      # Target language code
DEFAULT_TRANSLATE = True                     # Translate to English
DEFAULT_IDENTIFIER = "whisper"               # AI subtitle marker
DEFAULT_REGENERATE_AI = True                 # Re-process AI subtitles
DEFAULT_FORCE = False                        # Generate subtitles even if they exist
DEFAULT_WORD_TIMESTAMPS = False               # Enable word-level timestamps (slower but more precise)

# ============================================================================
# CONSTANTS
# ============================================================================

VIDEO_EXTENSIONS = {'.mp4', '.mkv', '.avi', '.mov', '.wmv', '.flv', '.webm', '.m4v', '.mpg', '.mpeg'}
SUBTITLE_EXTENSIONS = {'.srt', '.vtt', '.ass', '.ssa', '.sub'}

# ============================================================================
# CORE FUNCTIONS
# ============================================================================

def has_subtitles(video_path, skip_ai_generated=False, ai_identifier="whisper"):
    """Check if a video file already has subtitles."""
    video_stem = video_path.stem
    video_dir = video_path.parent
    
    for sub_ext in SUBTITLE_EXTENSIONS:
        try:
            # Check exact match
            subtitle_file = video_dir / f"{video_stem}{sub_ext}"
            if subtitle_file.exists():
                if skip_ai_generated and ai_identifier in subtitle_file.name:
                    continue
                return True
            
            # Check with language codes
            for lang_code in ['en', 'eng', 'english']:
                subtitle_file = video_dir / f"{video_stem}.{lang_code}{sub_ext}"
                if subtitle_file.exists():
                    if skip_ai_generated and ai_identifier in subtitle_file.name:
                        continue
                    return True
                
                # Check with AI identifier
                subtitle_file = video_dir / f"{video_stem}.{lang_code}.{ai_identifier}{sub_ext}"
                if subtitle_file.exists():
                    if skip_ai_generated:
                        continue
                    return True
        except OSError as e:
            if e.errno == 36:  # Filename too long
                continue
            raise
    
    return False


def find_video_files(media_folder):
    """Recursively find all video files in a folder."""
    video_files = []
    
    for root, dirs, files in os.walk(media_folder):
        for file in files:
            file_path = Path(root) / file
            if file_path.suffix.lower() in VIDEO_EXTENSIONS:
                video_files.append(file_path)
    
    return video_files


def generate_subtitle(video_path, model, output_format='srt', target_language='en',
                     translate=False, ai_identifier="whisper", word_timestamps=False):
    """Generate a subtitle file for a video using Whisper."""
    try:
        print(f"\n▶ Processing: {video_path.name}")
        
        # Transcribe/translate
        task = 'translate' if translate else 'transcribe'
        transcribe_params = {
            'audio': str(video_path),
            'task': task,
            'verbose': False,
            'word_timestamps': word_timestamps
        }
        
        if not translate:
            transcribe_params['language'] = target_language
        
        result = model.transcribe(**transcribe_params)
        
        # Build subtitle filename
        video_stem = video_path.stem
        
        if ai_identifier:
            subtitle_filename = f"{video_stem}.{target_language}.{ai_identifier}.{output_format}"
        else:
            subtitle_filename = f"{video_stem}.{target_language}.{output_format}"
        
        subtitle_path = video_path.parent / subtitle_filename
        
        # Handle long filenames
        max_length = 255
        if len(subtitle_filename) > max_length:
            suffix_length = len(f".{target_language}.{ai_identifier}.{output_format}") if ai_identifier else len(f".{target_language}.{output_format}")
            available = max_length - suffix_length - 3
            short_stem = video_stem[:available] + "..."
            
            if ai_identifier:
                subtitle_filename = f"{short_stem}.{target_language}.{ai_identifier}.{output_format}"
            else:
                subtitle_filename = f"{short_stem}.{target_language}.{output_format}"
            
            subtitle_path = video_path.parent / subtitle_filename
            print(f"  ⚠ Long filename, shortened to: {subtitle_path.name}")
        
        # Write subtitle file using Whisper's writer
        from whisper.utils import get_writer, WriteSRT, WriteVTT, WriteTXT, WriteJSON
        
        # Create a temporary writer to generate content
        output_dir = str(video_path.parent)
        
        # Generate subtitle content with a temporary name
        temp_name = f"_temp_whisper_{os.getpid()}"
        writer = get_writer(output_format, output_dir)
        writer(result, temp_name)
        
        # Find the temp file
        temp_file = video_path.parent / f"{temp_name}.{output_format}"
        
        # Rename to desired filename with identifier
        if temp_file.exists():
            temp_file.rename(subtitle_path)
            print(f"  ✓ Saved: {subtitle_path.name}")
        else:
            print(f"  ✗ Error: Temporary file not created")
            return False
        
        return True
        
    except KeyboardInterrupt:
        print(f"\n  ⚠ Interrupted while processing {video_path.name}")
        raise
    except Exception as e:
        print(f"  ✗ Error: {str(e)}")
        return False


def process_folder(folder_path, model, args):
    """Process all videos in a folder."""
    print(f"\n{'='*70}")
    print(f"Processing: {folder_path}")
    print(f"{'='*70}")
    
    # Find files
    video_files = find_video_files(folder_path)
    print(f"Found {len(video_files)} video files")
    
    if len(video_files) == 0:
        print("No video files found.")
        return 0, 0, 0
    
    # Filter files
    files_to_process = []
    files_with_subtitles = []
    
    skip_ai = args.regenerate_ai
    
    for video_file in video_files:
        if has_subtitles(video_file, skip_ai_generated=skip_ai, ai_identifier=args.identifier):
            files_with_subtitles.append(video_file)
            if not args.skip_existing:
                files_to_process.append(video_file)
        else:
            files_to_process.append(video_file)
    
    print(f"Files with existing subtitles: {len(files_with_subtitles)}")
    print(f"Files to process: {len(files_to_process)}")
    
    if len(files_to_process) == 0:
        print("All files already have subtitles.")
        return 0, 0, len(files_with_subtitles)
    
    # Dry run mode
    if args.dry_run:
        print("\n=== DRY RUN MODE ===")
        print("Would process:")
        for video_file in files_to_process[:10]:  # Show first 10
            print(f"  - {video_file.relative_to(folder_path)}")
        if len(files_to_process) > 10:
            print(f"  ... and {len(files_to_process) - 10} more")
        return 0, 0, len(files_with_subtitles)
    
    # Process files
    print(f"\nGenerating subtitles... (Press Ctrl+C to stop)")
    success_count = 0
    failure_count = 0
    
    try:
        for video_file in tqdm(files_to_process, desc="Progress", unit="file"):
            if generate_subtitle(video_file, model, args.format, args.language,
                               args.translate, args.identifier, args.word_timestamps):
                success_count += 1
            else:
                failure_count += 1
    except KeyboardInterrupt:
        print("\n\n⚠ Processing interrupted!")
        skipped = len(files_to_process) - (success_count + failure_count)
        print(f"\nPartial results:")
        print(f"  Successful: {success_count}")
        print(f"  Failed: {failure_count}")
        print(f"  Skipped: {skipped}")
        raise
    
    return success_count, failure_count, len(files_with_subtitles)


def main():
    parser = argparse.ArgumentParser(
        description='Generate subtitles for media files using OpenAI Whisper',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Process configured folders with default settings
  python batch_generate.py
  
  # Process a specific folder
  python batch_generate.py /mnt/jellyfin/anime
  
  # Use different model and settings
  python batch_generate.py /media --model base --no-translate
  
  # Dry run to preview
  python batch_generate.py /media --dry-run
  
  # Regenerate AI subtitles with better model
  python batch_generate.py /media --model small --regenerate-ai

Configuration:
  Edit DEFAULT_* variables at the top of this script to set defaults.
        """
    )
    
    # Positional arguments
    parser.add_argument(
        'folders',
        nargs='*',
        help='Media folders to process (overrides configured folders)'
    )
    
    # Model settings
    parser.add_argument(
        '--model', '-m',
        default=DEFAULT_MODEL,
        choices=['tiny', 'base', 'small', 'medium', 'large', 'turbo'],
        help=f'Whisper model size (default: {DEFAULT_MODEL}). '
             'tiny=~10x speed, turbo=~8x speed, base=~7x, small=~4x, medium=~2x, large=1x (slowest/best)'
    )
    
    # Output settings
    parser.add_argument(
        '--format', '-f',
        default=DEFAULT_FORMAT,
        choices=['srt', 'vtt', 'txt', 'json'],
        help=f'Subtitle format (default: {DEFAULT_FORMAT})'
    )
    
    parser.add_argument(
        '--language', '-l',
        default=DEFAULT_LANGUAGE,
        help=f'Target language code (default: {DEFAULT_LANGUAGE}). '
             'Examples: en, es, fr, de, ja, ko, zh'
    )
    
    # Translation settings
    parser.add_argument(
        '--translate', '-t',
        dest='translate',
        action='store_true',
        default=DEFAULT_TRANSLATE,
        help='Translate audio to English (default if configured)'
    )
    
    parser.add_argument(
        '--no-translate',
        dest='translate',
        action='store_false',
        help='Transcribe in original language instead of translating'
    )
    
    # AI identifier
    parser.add_argument(
        '--identifier', '-i',
        default=DEFAULT_IDENTIFIER,
        help=f'AI subtitle identifier (default: "{DEFAULT_IDENTIFIER}"). '
             'Use "" to disable. Creates: video.en.whisper.srt'
    )
    
    # Processing options
    parser.add_argument(
        '--regenerate-ai',
        action='store_true',
        default=DEFAULT_REGENERATE_AI,
        help='Regenerate AI-generated subtitles even if they exist'
    )
    
    parser.add_argument(
        '--skip-existing',
        action='store_true',
        default=not DEFAULT_FORCE,
        help='Skip files with existing subtitles (default: enabled unless DEFAULT_FORCE is True)'
    )
    
    parser.add_argument(
        '--no-skip-existing',
        dest='skip_existing',
        action='store_false',
        help='Process all files, even if subtitles exist'
    )
    
    parser.add_argument(
        '--force',
        dest='skip_existing',
        action='store_false',
        help='Force generation even if subtitles exist (alias for --no-skip-existing)'
    )
    
    parser.add_argument(
        '--dry-run', '-n',
        action='store_true',
        help='Preview which files would be processed without generating'
    )
    
    parser.add_argument(
        '--word-timestamps',
        action='store_true',
        default=DEFAULT_WORD_TIMESTAMPS,
        help='Enable word-level timestamps for more precise timing (slower processing)'
    )
    
    args = parser.parse_args()
    
    # Folders to process
    if args.folders:
        folders_to_process = [Path(f) for f in args.folders]
    elif DEFAULT_MEDIA_FOLDERS:
        folders_to_process = [Path(f) for f in DEFAULT_MEDIA_FOLDERS]
    else:
        print("ERROR: No folders specified!")
        print("\nEither:")
        print("  1. Edit DEFAULT_MEDIA_FOLDERS in this script")
        print("  2. Provide folder paths as arguments")
        print("\nExample: python batch_generate.py /mnt/jellyfin/anime")
        sys.exit(1)
    
    # Validate
    valid_folders = []
    for folder in folders_to_process:
        if folder.exists() and folder.is_dir():
            valid_folders.append(folder)
        else:
            print(f"Warning: Folder not found or not a directory: {folder}")
    
    if not valid_folders:
        print("ERROR: No valid folders to process!")
        sys.exit(1)
    
    # Show configuration
    print("=" * 70)
    print("WHISPER SUBTITLE GENERATOR")
    print("=" * 70)
    print(f"Model: {args.model}")
    print(f"Format: {args.format}")
    print(f"Language: {args.language}")
    print(f"Mode: {'Translation to English' if args.translate else 'Transcription'}")
    print(f"AI Identifier: {args.identifier if args.identifier else '(none)'}")
    print(f"Regenerate AI: {args.regenerate_ai}")
    print(f"Skip Existing: {args.skip_existing}")
    print(f"Word Timestamps: {args.word_timestamps}")
    print(f"Dry Run: {args.dry_run}")
    print(f"Folders: {len(valid_folders)}")
    
    # Load Whisper model
    if not args.dry_run:
        print(f"\nLoading Whisper '{args.model}' model...")
        try:
            model = whisper.load_model(args.model)
            print("✓ Model loaded successfully")
        except Exception as e:
            print(f"✗ Error loading model: {e}")
            sys.exit(1)
    else:
        model = None
    
    # Process each folder
    total_success = 0
    total_failed = 0
    total_skipped = 0
    
    try:
        for folder in valid_folders:
            success, failed, skipped = process_folder(folder, model, args)
            total_success += success
            total_failed += failed
            total_skipped += skipped
    except KeyboardInterrupt:
        print("\n\n⚠ Batch processing interrupted by user!")
    
    # Final summary
    print("\n" + "=" * 70)
    print("BATCH PROCESSING COMPLETE")
    print("=" * 70)
    print(f"Total folders processed: {len(valid_folders)}")
    print(f"Successful: {total_success}")
    print(f"Failed: {total_failed}")
    print(f"Already had subtitles: {total_skipped}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nExiting...")
        sys.exit(130)
