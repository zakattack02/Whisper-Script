#!/bin/bash
# make-release.sh - builds whisper.cpp + C# plugin, packages, and releases
# Usage: bash make-release.sh [--dry-run]

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
    echo -e "${RED}\u2717 $1${NC}"
    exit 1
}

# Parse --dry-run flag
DRY_RUN=false
for arg in "$@"; do
    [ "$arg" = "--dry-run" ] && DRY_RUN=true
done

# ── Preflight: dirty-tree check ───────────────────────────────────────────────
if ! git diff-index --quiet HEAD; then
    error_exit "Working tree has uncommitted changes. Commit or stash first."
fi
if ! git diff-index --cached --quiet HEAD; then
    error_exit "Staged but uncommitted changes. Commit first."
fi

# ── Preflight: dependencies ───────────────────────────────────────────────────
clear
echo -e "${BLUE}╔═══════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Whisper Subtitles Plugin - Release Wizard            ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${YELLOW}[0/9] Checking dependencies...${NC}"
MISSING=()
for cmd in git cmake dotnet zip python3 sha256sum; do
    if ! command -v "$cmd" &>/dev/null; then
        MISSING+=("$cmd")
    fi
done
if [ ${#MISSING[@]} -ne 0 ]; then
    error_exit "Missing required tools: ${MISSING[*]}\n  Install: sudo apt-get install -y git cmake build-essential dotnet-sdk-9.0 zip python3"
fi
echo -e "${GREEN}\u2713 All dependencies present${NC}"
echo ""

# ── Version selection ─────────────────────────────────────────────────────────
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' "$BUILD_PROPS_FILE")
[ -z "$CURRENT_VERSION" ] && error_exit "Could not read version from $BUILD_PROPS_FILE"

# Check if tag already exists
if git tag -l "v${CURRENT_VERSION}" | grep -q .; then
    error_exit "Tag v${CURRENT_VERSION} already exists. Increment version first."
fi

echo -e "${CYAN}Current version: ${GREEN}$CURRENT_VERSION${NC}"
echo ""
echo -e "${YELLOW}How would you like to increment the version?${NC}"
echo "  1) Revision (3.0.0.0 \u2192 3.0.0.1 — hotfix/bug-fix)"
echo "  2) Patch    (3.0.0.0 \u2192 3.0.1.0 — minor fix)"
echo "  3) Minor    (3.0.0.0 \u2192 3.1.0.0 — new feature)"
echo "  4) Major    (3.0.0.0 \u2192 4.0.0.0 — breaking change)"
echo "  5) Manual entry"
echo "  6) Use current version as-is"
echo ""
read -p "Select option (1-6): " VERSION_OPTION

case $VERSION_OPTION in
    1) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$NF=$NF+1; print}' OFS=.) ;;
    2) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-1)=$(NF-1)+1; $NF=0; print}' OFS=.) ;;
    3) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-2)=$(NF-2)+1; $(NF-1)=0; $NF=0; print}' OFS=.) ;;
    4) NEW_VERSION=$(echo "$CURRENT_VERSION" | awk -F. '{$(NF-3)=$(NF-3)+1; $(NF-2)=0; $(NF-1)=0; $NF=0; print}' OFS=.) ;;
    5) read -p "Enter version (e.g., 3.0.1.0): " NEW_VERSION ;;
    6) NEW_VERSION="$CURRENT_VERSION" ;;
    *) error_exit "Invalid option" ;;
esac

echo -e "${GREEN}Version: $CURRENT_VERSION \u2192 $NEW_VERSION${NC}"
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

if $DRY_RUN; then
    echo -e "${YELLOW}--dry-run: stopping before build${NC}"
    echo -e "  Would build v${NEW_VERSION}: ${CHANGELOG_ONELINE}"
    exit 0
fi

# ── Step 1: Update version ────────────────────────────────────────────────────
echo -e "${YELLOW}[1/9] Updating version...${NC}"
sed -i "s|<Version>.*</Version>|<Version>$NEW_VERSION</Version>|" "$BUILD_PROPS_FILE"
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|" "$BUILD_PROPS_FILE"
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$NEW_VERSION</FileVersion>|" "$BUILD_PROPS_FILE"
echo -e "${GREEN}\u2713 Version updated${NC}"
echo ""

# ── Step 2: Clean ─────────────────────────────────────────────────────────────
echo -e "${YELLOW}[2/9] Cleaning previous builds...${NC}"
rm -rf "${PLUGIN_FOLDER}/bin/${BUILD_CONFIG}"
rm -rf "${PLUGIN_FOLDER}/obj"
mkdir -p "$RELEASES_DIR"
echo -e "${GREEN}\u2713 Cleaned${NC}"
echo ""

# ── Step 3: Build whisper.cpp ─────────────────────────────────────────────────
echo -e "${YELLOW}[3/9] Building whisper.cpp using Build-whisper.sh...${NC}"

# Create output directory for the binary (use absolute path)
WHISPER_OUTPUT="$(cd "$PLUGIN_FOLDER" && pwd)/bin/whisper/linux-x64"
mkdir -p "$WHISPER_OUTPUT"

# Docker is now the default build method for GLIBC compatibility
# Use --no-docker flag to force native build if needed
bash "${PLUGIN_FOLDER}/../Scripts/Build-whisper.sh" "$WHISPER_OUTPUT" "/tmp/whisper-cache" || {
    error_exit "whisper.cpp build failed"
}

[ ! -f "$WHISPER_OUTPUT/whisper-whisper-cli" ] && error_exit "CPU whisper binary not found at $WHISPER_OUTPUT/whisper-whisper-cli"

HAS_CUDA=false
if [ -f "$WHISPER_OUTPUT/whisper-whisper-cli-cuda" ]; then
    HAS_CUDA=true
    echo -e "${GREEN}  \u2713 CUDA binary also built: whisper-whisper-cli-cuda${NC}"
else
    echo -e "${YELLOW}  \u26a0 CUDA binary not produced (GPU support unavailable)${NC}"
fi

echo -e "${GREEN}\u2713 whisper.cpp built${NC}"
echo ""

# ── Step 4: Verify binaries ──────────────────────────────────────────────────
echo -e "${YELLOW}[4/9] Verifying whisper binaries...${NC}"

# Docker creates files as root — fix ownership if possible
if command -v sudo &>/dev/null; then
    sudo chown -R "$(id -u):$(id -g)" "$WHISPER_OUTPUT" 2>/dev/null || true
fi

[ -f "$WHISPER_OUTPUT/whisper-whisper-cli" ] || error_exit "CPU binary not found at $WHISPER_OUTPUT/whisper-whisper-cli"
chmod +x "$WHISPER_OUTPUT/whisper-whisper-cli" 2>/dev/null || true
echo -e "${GREEN}\u2713 CPU binary verified: $WHISPER_OUTPUT/whisper-whisper-cli ($(du -h "$WHISPER_OUTPUT/whisper-whisper-cli" | cut -f1))${NC}"

if $HAS_CUDA; then
    chmod +x "$WHISPER_OUTPUT/whisper-whisper-cli-cuda" 2>/dev/null || true
    echo -e "${GREEN}\u2713 CUDA binary verified: $WHISPER_OUTPUT/whisper-whisper-cli-cuda ($(du -h "$WHISPER_OUTPUT/whisper-whisper-cli-cuda" | cut -f1))${NC}"
    
    for lib in libcudart.so.12 libcublas.so.12 libcublasLt.so.12; do
        if [ -f "$WHISPER_OUTPUT/$lib" ]; then
            chmod +x "$WHISPER_OUTPUT/$lib" 2>/dev/null || true
            echo "  \u2713 $lib ($(du -h "$WHISPER_OUTPUT/$lib" | cut -f1))"
        else
            echo -e "${YELLOW}  \u26a0 $lib missing — CUDA binary may not function${NC}"
        fi
    done
else
    echo -e "${YELLOW}  (CUDA binary not available — GPU support disabled)${NC}"
fi

echo ""

# ── Step 5: Build C# plugin ───────────────────────────────────────────────────
echo -e "${YELLOW}[5/9] Building C# plugin...${NC}"
dotnet publish \
    --configuration "$BUILD_CONFIG" \
    "${PLUGIN_FOLDER}/${PLUGIN_NAME}.csproj" \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary

[ ! -d "$BUILD_OUTPUT" ] && error_exit "Build output not found: $BUILD_OUTPUT"

# Copy whisper binaries and CUDA libs into publish output
echo "  Copying whisper binaries and CUDA libraries into publish output..."
mkdir -p "$BUILD_OUTPUT/whisper/linux-x64"
cp "$WHISPER_OUTPUT/whisper-whisper-cli" "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli"
chmod +x "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli" 2>/dev/null || true

HAS_CUDA=false
if [ -f "$WHISPER_OUTPUT/whisper-whisper-cli-cuda" ]; then
    HAS_CUDA=true
    cp "$WHISPER_OUTPUT/whisper-whisper-cli-cuda" "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli-cuda"
    chmod +x "$BUILD_OUTPUT/whisper/linux-x64/whisper-whisper-cli-cuda" 2>/dev/null || true
    
    for lib in libcudart.so.12 libcublas.so.12 libcublasLt.so.12; do
        if [ -f "$WHISPER_OUTPUT/$lib" ]; then
            cp "$WHISPER_OUTPUT/$lib" "$BUILD_OUTPUT/whisper/linux-x64/$lib"
        fi
    done
fi

echo -e "${GREEN}\u2713 C# plugin built${NC}"
echo ""

# ── Step 6: Package zips ──────────────────────────────────────────────────────
PACKAGE_NAME="jellyfin-plugin-whispersubtitles_${NEW_VERSION}.zip"
PACKAGE_PATH="$(pwd)/${RELEASES_DIR}/${PACKAGE_NAME}"

echo -e "${YELLOW}[6/9] Creating packages...${NC}"

# Combined zip (CPU + CUDA) — manifest default, backward compatible
[ -f "$PACKAGE_PATH" ] && rm -f "$PACKAGE_PATH"
(cd "$BUILD_OUTPUT" && zip -q -r "$PACKAGE_PATH" .)
[ ! -f "$PACKAGE_PATH" ] && error_exit "Failed to create zip"

CHECKSUM_MD5=$(md5sum "$PACKAGE_PATH" | awk '{print $1}')
CHECKSUM_SHA256=$(sha256sum "$PACKAGE_PATH" | awk '{print $1}')
FILESIZE=$(du -h "$PACKAGE_PATH" | cut -f1)

echo -e "${GREEN}\u2713 Combined package created (${FILESIZE}, MD5: ${CHECKSUM_MD5})${NC}"

# CPU-only zip (no CUDA binaries) — for users without NVIDIA GPUs
if $HAS_CUDA; then
    CPU_PACKAGE_NAME="jellyfin-plugin-whispersubtitles_${NEW_VERSION}_cpu.zip"
    CPU_PACKAGE_PATH="$(pwd)/${RELEASES_DIR}/${CPU_PACKAGE_NAME}"
    
    # Create a temp copy of the publish output without CUDA files
    CPU_OUTPUT="${BUILD_OUTPUT}_cpu"
    rm -rf "$CPU_OUTPUT"
    mkdir -p "$CPU_OUTPUT/whisper/linux-x64"
    
    # Copy everything except CUDA binaries
    rsync -a --exclude='whisper/linux-x64/whisper-whisper-cli-cuda' --exclude='whisper/linux-x64/libcudart.so.12' --exclude='whisper/linux-x64/libcublas.so.12' --exclude='whisper/linux-x64/libcublasLt.so.12' "$BUILD_OUTPUT/" "$CPU_OUTPUT/"
    
    [ -f "$CPU_PACKAGE_PATH" ] && rm -f "$CPU_PACKAGE_PATH"
    (cd "$CPU_OUTPUT" && zip -q -r "$CPU_PACKAGE_PATH" .)
    rm -rf "$CPU_OUTPUT"
    
    CPU_CHECKSUM_MD5=$(md5sum "$CPU_PACKAGE_PATH" | awk '{print $1}')
    CPU_CHECKSUM_SHA256=$(sha256sum "$CPU_PACKAGE_PATH" | awk '{print $1}')
    CPU_FILESIZE=$(du -h "$CPU_PACKAGE_PATH" | cut -f1)
    
    echo -e "${GREEN}\u2713 CPU-only package created (${CPU_FILESIZE}, MD5: ${CPU_CHECKSUM_MD5})${NC}"
    echo -e "${YELLOW}  \u2192 Manifest points to combined (CPU+CUDA) zip${NC}"
    echo -e "${YELLOW}  \u2192 CPU-only zip published as additional asset${NC}"
fi

echo ""

# ── Step 7: Update manifest.json ─────────────────────────────────────────────
echo -e "${YELLOW}[7/9] Updating manifest.json...${NC}"
TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)
CHANGELOG_ESCAPED=$(echo "$CHANGELOG_ONELINE" | sed 's/"/\\"/g')

python3 - "$MANIFEST_FILE" "$NEW_VERSION" "$CHANGELOG_ESCAPED" "$CHECKSUM_MD5" "$TIMESTAMP" "$PACKAGE_NAME" <<'PYTHON_EOF'
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

echo -e "${GREEN}\u2713 Manifest updated${NC}"
echo ""

# ── Step 8: Local commit + tag ────────────────────────────────────────────────
echo -e "${YELLOW}[8/9] Committing and tagging...${NC}"

git add "$MANIFEST_FILE" "$BUILD_PROPS_FILE"
git commit -m "Release v${NEW_VERSION}: ${CHANGELOG_ONELINE}"
git tag "v${NEW_VERSION}"

echo -e "${GREEN}\u2713 Committed and tagged locally${NC}"
echo ""

# ── Step 9: GitHub release (push only after successful release) ───────────────
echo -e "${YELLOW}[9/9] Publishing to GitHub...${NC}"

RELEASE_NOTES="$CHANGELOG_ONELINE"
RELEASE_CREATED=false

if command -v gh &>/dev/null && command -v git &>/dev/null; then
    if $HAS_CUDA && [ -f "$CPU_PACKAGE_PATH" ]; then
        if gh release create "v${NEW_VERSION}" "$PACKAGE_PATH" "$CPU_PACKAGE_PATH" \
            --title "v${NEW_VERSION}" \
            --notes "$RELEASE_NOTES"; then
            RELEASE_CREATED=true
            echo -e "${GREEN}\u2713 Uploaded combined + CPU-only zips${NC}"
        fi
    else
        if gh release create "v${NEW_VERSION}" "$PACKAGE_PATH" \
            --title "v${NEW_VERSION}" \
            --notes "$RELEASE_NOTES"; then
            RELEASE_CREATED=true
        fi
    fi

    if $RELEASE_CREATED; then
        git push origin HEAD
        git push origin "v${NEW_VERSION}"
        RELEASE_URL=$(gh release view "v${NEW_VERSION}" --json url --jq .url 2>/dev/null)
        echo -e "${GREEN}\u2713 GitHub release created: ${CYAN}${RELEASE_URL}${NC}"
    else
        echo -e "${RED}\u2717 GitHub release creation failed — tag and commit kept locally${NC}"
        echo -e "${YELLOW}  Fix the issue, delete the tag with: git tag -d v${NEW_VERSION}${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}\u26a0 gh CLI not found — pushing anyway, create release manually:${NC}"
    git push origin HEAD
    git push origin "v${NEW_VERSION}"
    echo "    gh release create v${NEW_VERSION} ${PACKAGE_PATH} --title v${NEW_VERSION} --notes '${CHANGELOG_ONELINE}'"
fi
echo ""

# ── Done ──────────────────────────────────────────────────────────────────────
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}[DONE] v${NEW_VERSION} released successfully${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
echo -e "  Combined package: ${BLUE}${PACKAGE_PATH}${NC}"
echo -e "  Size:             ${BLUE}${FILESIZE}${NC}"
echo -e "  MD5:              ${BLUE}${CHECKSUM_MD5}${NC}"
echo -e "  SHA256:           ${BLUE}${CHECKSUM_SHA256}${NC}"
if [ -n "$CPU_PACKAGE_PATH" ]; then
    echo -e "  CPU-only package: ${BLUE}${CPU_PACKAGE_PATH}${NC}"
    echo -e "  CPU-only size:    ${BLUE}${CPU_FILESIZE}${NC}"
fi
echo ""
echo -e "${CYAN}Install in Jellyfin:${NC}"
echo "  Dashboard \u2192 Plugins \u2192 Repositories \u2192 Add"
echo "  Or manually upload the zip"
echo ""
