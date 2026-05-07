#!/bin/bash
# make-release.sh - builds whisper.cpp + C# plugin, packages, and releases

set -e  # Exit on error — don't hide failures

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
PLUGIN_NAME="Jellyfin.Plugin.WhisperSubtitles"
PLUGIN_FOLDER="${PLUGIN_NAME}/${PLUGIN_NAME}"
BUILD_CONFIG="Release"
TARGET_FRAMEWORK="net9.0"
RELEASES_DIR="releases"
MANIFEST_FILE="manifest.json"
BUILD_PROPS_FILE="Directory.Build.props"
WHISPER_REPO="/tmp/whisper.cpp"
WHISPER_BINARY_DIR="${PLUGIN_FOLDER}/bin/whisper/linux-x64"
BUILD_OUTPUT="${PLUGIN_FOLDER}/bin/${BUILD_CONFIG}/${TARGET_FRAMEWORK}/publish"

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

# ── Preflight checks ─────────────────────────────────────────────────────────
echo -e "${YELLOW}[0/8] Checking dependencies...${NC}"
MISSING=()
for cmd in git cmake dotnet zip python3 md5sum; do
    if ! command -v "$cmd" &>/dev/null; then
        MISSING+=("$cmd")
    fi
done
if [ ${#MISSING[@]} -ne 0 ]; then
    error_exit "Missing required tools: ${MISSING[*]}\n  Install: sudo apt-get install -y git cmake build-essential dotnet-sdk-9.0 zip python3"
fi
echo -e "${GREEN}✓ All dependencies present${NC}"
echo ""

# ── Version selection ─────────────────────────────────────────────────────────
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' "$BUILD_PROPS_FILE")
[ -z "$CURRENT_VERSION" ] && error_exit "Could not read version from $BUILD_PROPS_FILE"

echo -e "${CYAN}Current version: ${GREEN}$CURRENT_VERSION${NC}"
echo ""
echo -e "${YELLOW}How would you like to increment the version?${NC}"
echo "  1) Patch (0.0.0.47 → 0.0.0.48)"
echo "  2) Minor (0.0.0.47 → 0.0.1.0)"
echo "  3) Major (0.0.0.47 → 0.1.0.0)"
echo "  4) Manual entry"
echo "  5) Use current version as-is"
echo ""
read -p "Select option (1-5): " VERSION_OPTION

case $VERSION_OPTION in
    1) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$NF=$NF+1; print}' OFS=.) ;;
    2) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-1)=$(NF-1)+1; $NF=0; print}' OFS=.) ;;
    3) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-2)=$(NF-2)+1; $(NF-1)=0; $NF=0; print}' OFS=.) ;;
    4) read -p "Enter version (e.g., 0.0.0.48): " NEW_VERSION ;;
    5) NEW_VERSION="$CURRENT_VERSION" ;;
    *) error_exit "Invalid option" ;;
esac

echo -e "${GREEN}Version: $CURRENT_VERSION → $NEW_VERSION${NC}"
echo ""

# ── Changelog ────────────────────────────────────────────────────────────────
echo -e "${YELLOW}Enter changelog (Ctrl+D when done):${NC}"
CHANGELOG=""
if [ -t 0 ]; then
    while IFS= read -r line; do
        CHANGELOG="${CHANGELOG:+$CHANGELOG$'\n'}$line"
    done
else
    read CHANGELOG
fi
[ -z "$CHANGELOG" ] && CHANGELOG="Bug fixes and improvements"
CHANGELOG_ONELINE=$(echo "$CHANGELOG" | head -1)

# ── Confirm ───────────────────────────────────────────────────────────────────
echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "  Version:   ${GREEN}$NEW_VERSION${NC}"
echo -e "  Changelog: ${GREEN}$CHANGELOG_ONELINE${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
read -p "Continue? (y/n): " -n 1 REPLY
echo ""
[[ ! $REPLY =~ ^[Yy]$ ]] && { echo -e "${RED}Cancelled.${NC}"; exit 1; }
echo ""

# ── Step 1: Update version ────────────────────────────────────────────────────
echo -e "${YELLOW}[1/8] Updating version...${NC}"
sed -i "s|<Version>.*</Version>|<Version>$NEW_VERSION</Version>|" "$BUILD_PROPS_FILE"
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|" "$BUILD_PROPS_FILE"
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$NEW_VERSION</FileVersion>|" "$BUILD_PROPS_FILE"
echo -e "${GREEN}✓ Version updated${NC}"
echo ""

# ── Step 2: Clean ─────────────────────────────────────────────────────────────
echo -e "${YELLOW}[2/8] Cleaning previous builds...${NC}"
rm -rf "${PLUGIN_FOLDER}/bin/${BUILD_CONFIG}"
rm -rf "${PLUGIN_FOLDER}/obj"
mkdir -p "$RELEASES_DIR"
echo -e "${GREEN}✓ Cleaned${NC}"
echo ""

# ── Step 3: Build whisper.cpp ─────────────────────────────────────────────────
echo -e "${YELLOW}[3/8] Building whisper.cpp using Build-whisper.sh...${NC}"

# Create output directory for the binary (use absolute path)
WHISPER_OUTPUT="$(cd "$PLUGIN_FOLDER" && pwd)/bin/whisper/linux-x64"
mkdir -p "$WHISPER_OUTPUT"

# Docker is now the default build method for GLIBC compatibility
# Use --no-docker flag to force native build if needed
# Example: bash make-release.sh (will use Docker by default)
# Or: bash Scripts/Build-whisper.sh --no-docker <output> (for native build)

# Call the build script with the plugin directory as output
# Docker is used by default if available; falls back to native build if not
bash "${PLUGIN_FOLDER}/../Scripts/Build-whisper.sh" "$WHISPER_OUTPUT" "/tmp/whisper-cache" || {
    error_exit "whisper.cpp build failed"
}

# Verify binary was built
[ ! -f "$WHISPER_OUTPUT/whisper-whisper-cli" ] && error_exit "whisper binary not found at $WHISPER_OUTPUT/whisper-whisper-cli"

echo -e "${GREEN}✓ whisper.cpp built${NC}"
echo ""

# ── Step 4: Verify binary ────────────────────────────────────────────────────
echo -e "${YELLOW}[4/8] Verifying whisper binary...${NC}"
# Note: WHISPER_OUTPUT was computed in Step 3
[ -f "$WHISPER_OUTPUT/whisper-whisper-cli" ] || error_exit "Binary not found at $WHISPER_OUTPUT/whisper-whisper-cli"
chmod +x "$WHISPER_OUTPUT/whisper-whisper-cli"
echo -e "${GREEN}✓ Binary verified: $WHISPER_OUTPUT/whisper-whisper-cli${NC}"
echo ""

# ── Step 5: Build C# plugin ───────────────────────────────────────────────────
echo -e "${YELLOW}[5/8] Building C# plugin...${NC}"
dotnet publish \
    --configuration "$BUILD_CONFIG" \
    "${PLUGIN_FOLDER}/${PLUGIN_NAME}.csproj" \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary

[ ! -d "$BUILD_OUTPUT" ] && error_exit "Build output not found: $BUILD_OUTPUT"

# Copy whisper binary into publish output so it ends up in the zip
echo "  Copying whisper binary into publish output..."
# Note: WHISPER_OUTPUT was computed in Step 3
mkdir -p "$BUILD_OUTPUT/whisper/linux-x64"
cp "$WHISPER_OUTPUT/whisper-whisper-cli" "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli"
chmod +x "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli"

echo -e "${GREEN}✓ C# plugin built${NC}"
echo ""

# ── Step 6: Package zip ───────────────────────────────────────────────────────
PACKAGE_NAME="jellyfin-plugin-whispersubtitles_${NEW_VERSION}.zip"
PACKAGE_PATH="$(pwd)/${RELEASES_DIR}/${PACKAGE_NAME}"

echo -e "${YELLOW}[6/8] Creating package: ${PACKAGE_NAME}${NC}"

[ -f "$PACKAGE_PATH" ] && rm -f "$PACKAGE_PATH"

(cd "$BUILD_OUTPUT" && zip -q -r "$PACKAGE_PATH" .)

[ ! -f "$PACKAGE_PATH" ] && error_exit "Failed to create zip"

CHECKSUM=$(md5sum "$PACKAGE_PATH" | awk '{print $1}')
FILESIZE=$(du -h "$PACKAGE_PATH" | cut -f1)

echo -e "${GREEN}✓ Package created (${FILESIZE}, MD5: ${CHECKSUM})${NC}"
echo ""

# ── Step 7: Update manifest.json ─────────────────────────────────────────────
echo -e "${YELLOW}[7/8] Updating manifest.json...${NC}"
TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)
CHANGELOG_ESCAPED=$(echo "$CHANGELOG_ONELINE" | sed 's/"/\\"/g')

python3 - "$MANIFEST_FILE" "$NEW_VERSION" "$CHANGELOG_ESCAPED" "$CHECKSUM" "$TIMESTAMP" "$PACKAGE_NAME" <<'PYTHON_EOF'
import json, sys

manifest_file, new_version, changelog, checksum, timestamp, package_name = sys.argv[1:]

with open(manifest_file) as f:
    manifest = json.load(f)

manifest[0]["versions"].insert(0, {
    "version": new_version,
    "changelog": changelog,
    "targetAbi": "10.11.2.0",
    "sourceUrl": f"https://github.com/zakattack02/Whisper-Script/releases/download/v{new_version}/{package_name}",
    "checksum": checksum,
    "timestamp": timestamp
})

with open(manifest_file, 'w') as f:
    json.dump(manifest, f, indent=2)

print("OK")
PYTHON_EOF

echo -e "${GREEN}✓ Manifest updated${NC}"
echo ""

# ── Step 8: GitHub release ────────────────────────────────────────────────────
echo -e "${YELLOW}[8/8] Publishing to GitHub...${NC}"

if command -v gh &>/dev/null && command -v git &>/dev/null; then
    git add "$MANIFEST_FILE" "$BUILD_PROPS_FILE"
    git commit -m "Release v${NEW_VERSION}: ${CHANGELOG_ONELINE}"
    git tag "v${NEW_VERSION}"
    git push origin HEAD
    git push origin "v${NEW_VERSION}"

    gh release create "v${NEW_VERSION}" "$PACKAGE_PATH" \
        --title "v${NEW_VERSION}" \
        --notes "$CHANGELOG_ONELINE"

    RELEASE_URL=$(gh release view "v${NEW_VERSION}" --json url --jq .url 2>/dev/null)
    echo -e "${GREEN}✓ GitHub release created: ${CYAN}${RELEASE_URL}${NC}"
else
    echo -e "${YELLOW}⚠ gh CLI not found — manual steps:${NC}"
    echo "    git add manifest.json Directory.Build.props"
    echo "    git commit -m 'Release v${NEW_VERSION}'"
    echo "    git tag v${NEW_VERSION}"
    echo "    git push origin HEAD && git push origin v${NEW_VERSION}"
    echo "    gh release create v${NEW_VERSION} ${PACKAGE_PATH} --title v${NEW_VERSION} --notes '${CHANGELOG_ONELINE}'"
fi
echo ""

# ── Done ──────────────────────────────────────────────────────────────────────
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}[DONE] v${NEW_VERSION} released successfully${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
echo -e "  Package:  ${BLUE}${PACKAGE_PATH}${NC}"
echo -e "  Size:     ${BLUE}${FILESIZE}${NC}"
echo -e "  MD5:      ${BLUE}${CHECKSUM}${NC}"
echo ""
echo -e "${CYAN}Install in Jellyfin:${NC}"
echo "  Dashboard → Plugins → Repositories → Add"
echo "  Or manually upload the zip"
echo ""