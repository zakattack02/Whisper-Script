#!/bin/bash

# Full automated release workflow

# Don't exit immediately on error - handle them gracefully
set +e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
PLUGIN_NAME="Jellyfin.Plugin.WhisperSubtitles"
PLUGIN_FOLDER="${PLUGIN_NAME}/${PLUGIN_NAME}"
BUILD_CONFIG="Release"
TARGET_FRAMEWORK="net9.0"
RELEASES_DIR="releases"
MANIFEST_FILE="manifest.json"
BUILD_PROPS_FILE="Directory.Build.props"

# Error handling function
error_exit() {
    echo -e "${RED}✗ $1${NC}"
    exit 1
}

clear
echo -e "${BLUE}╔═══════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Whisper Subtitles Plugin - Release Wizard            ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════╝${NC}"
echo ""

# Get current version from Directory.Build.props
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' "$BUILD_PROPS_FILE")
if [ -z "$CURRENT_VERSION" ]; then
    error_exit "Could not read version from $BUILD_PROPS_FILE"
fi

echo -e "${CYAN}Current version in Directory.Build.props: ${GREEN}$CURRENT_VERSION${NC}"
echo ""

# Version increment options
echo -e "${YELLOW}How would you like to increment the version?${NC}"
echo "  1) Patch (0.0.0.33 → 0.0.0.34)"
echo "  2) Minor (0.0.0.33 → 0.0.1.0)"
echo "  3) Major (0.0.0.33 → 0.1.0.0)"
echo "  4) Manual entry"
echo "  5) Use current version as-is"
echo ""
read -p "Select option (1-5): " VERSION_OPTION

case $VERSION_OPTION in
    1)
        # Patch version increment
        NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$NF=$NF+1; print}' OFS=.)
        echo -e "${GREEN}Patch increment: $CURRENT_VERSION → $NEW_VERSION${NC}"
        ;;
    2)
        # Minor version increment
        NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-1)=$(NF-1)+1; $NF=0; print}' OFS=.)
        echo -e "${GREEN}Minor increment: $CURRENT_VERSION → $NEW_VERSION${NC}"
        ;;
    3)
        # Major version increment
        NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-2)=$(NF-2)+1; $(NF-1)=0; $NF=0; print}' OFS=.)
        echo -e "${GREEN}Major increment: $CURRENT_VERSION → $NEW_VERSION${NC}"
        ;;
    4)
        # Manual version
        read -p "Enter version number (e.g., 0.0.0.34): " NEW_VERSION
        echo -e "${GREEN}Manual version: $NEW_VERSION${NC}"
        ;;
    5)
        # Use current version
        NEW_VERSION="$CURRENT_VERSION"
        echo -e "${GREEN}Using current version: $NEW_VERSION${NC}"
        ;;
    *)
        error_exit "Invalid option"
        ;;
esac

echo ""
echo -e "${YELLOW}Enter changelog/description for this release:${NC}"
echo -e "${CYAN}(Press Ctrl+D when done, or Ctrl+C to cancel)${NC}"
CHANGELOG=""
# Only read changelog in interactive mode
if [ -t 0 ]; then
    # Interactive terminal
    while IFS= read -r line; do
        if [ -z "$CHANGELOG" ]; then
            CHANGELOG="$line"
        else
            CHANGELOG="$CHANGELOG
$line"
        fi
    done
else
    # Piped input - read one line for changelog
    read CHANGELOG
fi

# If no input, use default
if [ -z "$CHANGELOG" ]; then
    CHANGELOG="Bug fixes and improvements"
fi

# Clean up changelog (remove newlines for single-line display)
CHANGELOG_ONELINE=$(echo "$CHANGELOG" | head -1)

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}Release Summary:${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "  Version: ${GREEN}$NEW_VERSION${NC}"
echo -e "  Changelog: ${GREEN}$CHANGELOG_ONELINE${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
read -p "Continue with release? (y/n): " -n 1 REPLY
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${RED}Release cancelled.${NC}"
    exit 1
fi
echo ""

# Update Directory.Build.props with new version
echo -e "${YELLOW}[1/7]${NC} Updating version in Directory.Build.props..."
sed -i "s/<Version>$CURRENT_VERSION<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$BUILD_PROPS_FILE"
sed -i "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>$NEW_VERSION<\/AssemblyVersion>/" "$BUILD_PROPS_FILE"
sed -i "s/<FileVersion>.*<\/FileVersion>/<FileVersion>$NEW_VERSION<\/FileVersion>/" "$BUILD_PROPS_FILE"
echo -e "${GREEN}✓ Version updated${NC}"
echo ""

# Check if releases directory exists
if [ ! -d "$RELEASES_DIR" ]; then
    mkdir -p "$RELEASES_DIR"
fi

# Clean previous build artifacts
echo -e "${YELLOW}[2/7]${NC} Cleaning previous builds..."
rm -rf "${PLUGIN_FOLDER}/bin/${BUILD_CONFIG}"
rm -rf "${PLUGIN_FOLDER}/obj"
echo -e "${GREEN}✓ Cleaned${NC}"
echo ""

# Build the plugin
echo -e "${YELLOW}[3/7]${NC} Building plugin (Release configuration)..."
dotnet publish \
    --configuration=${BUILD_CONFIG} \
    "${PLUGIN_FOLDER}/${PLUGIN_NAME}.csproj" \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary > /dev/null 2>&1

if [ $? -ne 0 ]; then
    error_exit "Build failed!"
fi
echo -e "${GREEN}✓ Build successful${NC}"
echo ""

# Ensure the whisper/linux-x64/main directory is in the build output
WHISPER_DIR="Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles/bin/whisper/linux-x64/"
if [ -d "$WHISPER_DIR" ]; then
    cp -r "$WHISPER_DIR" "$BUILD_OUTPUT/"
    echo -e "${GREEN}✓ Added whisper/linux-x64/main to the build output${NC}"
else
    echo -e "${RED}✗ whisper/linux-x64/main directory not found${NC}"
    exit 1
fi

# Create release package
PACKAGE_NAME="jellyfin-plugin-whispersubtitles_${NEW_VERSION}.zip"
PACKAGE_PATH="$(cd "$RELEASES_DIR" 2>/dev/null && pwd)/${PACKAGE_NAME}"
BUILD_OUTPUT="${PLUGIN_FOLDER}/bin/${BUILD_CONFIG}/${TARGET_FRAMEWORK}/publish"

echo -e "${YELLOW}[4/7]${NC} Creating release package: ${PACKAGE_NAME}"

# Check if build output exists
if [ ! -d "$BUILD_OUTPUT" ]; then
    error_exit "Build output directory not found: $BUILD_OUTPUT"
fi

# Ensure releases directory exists
if [ ! -d "$RELEASES_DIR" ]; then
    mkdir -p "$RELEASES_DIR"
fi

# Remove old package if it exists
if [ -f "$PACKAGE_PATH" ]; then
    rm -f "$PACKAGE_PATH"
fi

# Create zip file with absolute path
cd "$BUILD_OUTPUT" || error_exit "Failed to change directory to $BUILD_OUTPUT"
zip -q -r "$PACKAGE_PATH" . 
ZIP_RESULT=$?
cd - > /dev/null || error_exit "Failed to change back directory"

if [ $ZIP_RESULT -ne 0 ] || [ ! -f "$PACKAGE_PATH" ]; then
    error_exit "Failed to create release package!"
fi

# Calculate checksum
CHECKSUM=$(md5sum "$PACKAGE_PATH" | awk '{print $1}')
FILESIZE=$(du -h "$PACKAGE_PATH" | cut -f1)

if [ -z "$CHECKSUM" ]; then
    error_exit "Failed to calculate checksum!"
fi

echo -e "${GREEN}✓ Package created${NC}"
echo -e "  File size: ${BLUE}$FILESIZE${NC}"
echo -e "  Checksum: ${BLUE}$CHECKSUM${NC}"
echo ""

# Update manifest.json
echo -e "${YELLOW}[5/7]${NC} Updating manifest.json..."
TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# Escape special characters in changelog for JSON
CHANGELOG_ESCAPED=$(echo "$CHANGELOG_ONELINE" | sed 's/"/\\"/g' | sed 's/\\/\\\\/g')

# Create a Python script to update the manifest
TEMP_PYTHON=$(mktemp)
cat > "$TEMP_PYTHON" <<'PYTHON_EOF'
import json
import sys

manifest_file = sys.argv[1]
new_version = sys.argv[2]
changelog = sys.argv[3]
checksum = sys.argv[4]
timestamp = sys.argv[5]
package_name = sys.argv[6]

# Load existing manifest
with open(manifest_file, 'r') as f:
    manifest = json.load(f)

# Create new version entry
new_entry = {
    "version": new_version,
    "changelog": changelog,
    "targetAbi": "10.11.2.0",
    "sourceUrl": f"https://github.com/zakattack02/Whisper-Script/releases/download/v{new_version}/{package_name}",
    "checksum": checksum,
    "timestamp": timestamp
}

# Insert at the beginning of versions
manifest[0]["versions"].insert(0, new_entry)

# Write back
with open(manifest_file, 'w') as f:
    json.dump(manifest, f, indent=2)

print("OK")
PYTHON_EOF

# Try using Python 3
if command -v python3 &> /dev/null; then
    RESULT=$(python3 "$TEMP_PYTHON" "$MANIFEST_FILE" "$NEW_VERSION" "$CHANGELOG_ESCAPED" "$CHECKSUM" "$TIMESTAMP" "$PACKAGE_NAME" 2>&1)
    if [ "$RESULT" = "OK" ]; then
        echo -e "${GREEN}✓ Manifest updated with version $NEW_VERSION${NC}"
    else
        error_exit "Failed to update manifest: $RESULT"
    fi
    rm -f "$TEMP_PYTHON"
else
    echo -e "${YELLOW}⚠ Python3 not found, using manual JSON update${NC}"
    
    # Backup original
    cp "$MANIFEST_FILE" "$MANIFEST_FILE.backup"
    
    # Read current manifest and prepare new one
    TEMP_MANIFEST=$(mktemp)
    
    # Use jq if available, otherwise use sed (basic)
    if command -v jq &> /dev/null; then
        jq ".versions |= [{version: \"$NEW_VERSION\", changelog: \"$CHANGELOG_ESCAPED\", targetAbi: \"10.11.2.0\", sourceUrl: \"https://github.com/zakattack02/Whisper-Script/releases/download/v${NEW_VERSION}/${PACKAGE_NAME}\", checksum: \"$CHECKSUM\", timestamp: \"$TIMESTAMP\"}] + ." "$MANIFEST_FILE" > "$TEMP_MANIFEST"
        mv "$TEMP_MANIFEST" "$MANIFEST_FILE"
        echo -e "${GREEN}✓ Manifest updated with version $NEW_VERSION${NC}"
    else
        error_exit "Python3 and jq not found - cannot update manifest.json"
    fi
fi
echo ""

# Create GitHub release
echo -e "${YELLOW}[6/7]${NC} Creating GitHub Release v${NEW_VERSION}..."

# Check if git and gh CLI are available
if command -v gh &> /dev/null && command -v git &> /dev/null; then
    git add "$MANIFEST_FILE" "$BUILD_PROPS_FILE" "$PACKAGE_PATH" 2>/dev/null
    git commit -m "Release v${NEW_VERSION}" 2>/dev/null
    
    if [ $? -eq 0 ]; then
        git tag "v${NEW_VERSION}" 2>/dev/null
        git push origin "v${NEW_VERSION}" 2>/dev/null
        
        # Create GitHub release
        gh release create "v${NEW_VERSION}" "$PACKAGE_PATH" \
            --title "v${NEW_VERSION}" \
            --notes "$CHANGELOG_ONELINE" 2>/dev/null
        
        if [ $? -eq 0 ]; then
            RELEASE_URL=$(gh release view "v${NEW_VERSION}" --json url --jq .url 2>/dev/null)
            echo -e "${GREEN}✓ GitHub Release v${NEW_VERSION} created successfully${NC}"
            echo -e "  ${CYAN}$RELEASE_URL${NC}"
        else
            echo -e "${YELLOW}⚠ GitHub CLI release creation failed, but git commit succeeded${NC}"
        fi
    else
        echo -e "${YELLOW}⚠ Git commit failed, files may need to be manually committed${NC}"
    fi
else
    echo -e "${YELLOW}⚠ GitHub CLI or Git not found, skipping automatic GitHub release creation${NC}"
    echo -e "  ${CYAN}Manual steps:${NC}"
    echo -e "    git tag v${NEW_VERSION}"
    echo -e "    git push origin v${NEW_VERSION}"
    echo -e "    Upload zip to: https://github.com/zakattack02/Whisper-Script/releases"
fi
echo ""

# Summary
echo -e "${YELLOW}[7/7]${NC} Finalizing..."
echo -e "${GREEN}✓ Release ${NEW_VERSION} created successfully!${NC}"
echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}[SUCCESS]${NC} Release ${NEW_VERSION} created successfully!"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""

echo -e "${CYAN}[INFO]${NC} Next steps:"
echo "  1. Review the changes:"
echo -e "     - ${BLUE}manifest.json${NC}"
echo -e "     - ${BLUE}$PACKAGE_PATH${NC}"
echo -e "     - ${BLUE}$BUILD_PROPS_FILE${NC}"
echo "  2. Test the plugin: $PACKAGE_PATH"
echo "  3. If not auto-pushed, commit and push:"
echo "     git push"
echo ""

echo -e "${CYAN}[INFO]${NC} File size: ${BLUE}$FILESIZE${NC}"
echo -e "${CYAN}[INFO]${NC} Location: ${BLUE}$PACKAGE_PATH${NC}"
echo ""

exit 0
