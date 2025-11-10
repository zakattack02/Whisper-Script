#!/usr/bin/env python3
"""
Download Whisper models to cache for faster loading later.
This downloads models once so they don't need to be downloaded during subtitle generation.
"""

import whisper
import sys

MODELS = ['tiny', 'base', 'small', 'medium', 'large', 'turbo']

def download_model(model_name):
    """Download a specific model."""
    print(f"\nDownloading '{model_name}' model...")
    try:
        whisper.load_model(model_name)
        print(f"✓ {model_name} model downloaded and cached")
        return True
    except Exception as e:
        print(f"✗ Error downloading {model_name}: {e}")
        return False

def main():
    if len(sys.argv) > 1:
        # Download specific models
        models_to_download = sys.argv[1:]
    else:
        # Prompt user
        print("=" * 70)
        print("WHISPER MODEL DOWNLOADER")
        print("=" * 70)
        print("\nAvailable models (Parameters | VRAM | Speed):")
        print("  tiny   - 39M  params | ~1 GB  VRAM | ~10x speed (fastest)")
        print("  base   - 74M  params | ~1 GB  VRAM | ~7x  speed")
        print("  small  - 244M params | ~2 GB  VRAM | ~4x  speed (recommended)")
        print("  medium - 769M params | ~5 GB  VRAM | ~2x  speed")
        print("  turbo  - 809M params | ~6 GB  VRAM | ~8x  speed (fast + quality)")
        print("  large  - 1550M params| ~10 GB VRAM | 1x   speed (best quality)")
        print("\nWhich models would you like to download?")
        print("Enter: 'all' for all models, or specific names separated by spaces")
        print("Example: small medium")
        
        choice = input("\nYour choice: ").strip().lower()
        
        if choice == 'all':
            models_to_download = MODELS
        else:
            models_to_download = [m.strip() for m in choice.split() if m.strip() in MODELS]
        
        if not models_to_download:
            print("No valid models selected. Exiting.")
            return
    
    print(f"\nWill download: {', '.join(models_to_download)}")
    print("=" * 70)
    
    success_count = 0
    for model_name in models_to_download:
        if download_model(model_name):
            success_count += 1
    
    print("\n" + "=" * 70)
    print(f"Downloaded {success_count}/{len(models_to_download)} models successfully")
    print("=" * 70)
    print("\nModels are cached at: ~/.cache/whisper/")

if __name__ == "__main__":
    main()
