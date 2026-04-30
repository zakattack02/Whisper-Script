#!/bin/bash
# Build-whisper.sh
# Builds whisper.cpp from source and stages the binary for plugin use.
#
# Usage:
#   bash Build-whisper.sh <OUTPUT_DIR> [CACHE_DIR]
#
# OUTPUT_DIR : Where the final binary lands (e.g. plugin's whisper/linux-x64/)
# CACHE_DIR  : Where to clone/cache the whisper.cpp source (default: /tmp/whisper-cache)

set -e

OUTPUT_DIR="${1:?ERROR: OUTPUT_DIR not specified. Usage: Build-whisper.sh <OUTPUT_DIR> [CACHE_DIR]}"
CACHE_DIR="${2:-/tmp/whisper-cache}"

# The name the plugin expects on disk inside its own directory.
# WhisperBinaryManager.FindBundledBinary() looks for this exact name.
BINARY_NAME="whisper-whisper-cli"

WHISPER_REPO="https://github.com/ggerganov/whisper.cpp"
WHISPER_SRC="${CACHE_DIR}/whisper.cpp"

echo "=== whisper.cpp Build Script ==="
echo "Output dir : ${OUTPUT_DIR}"
echo "Cache dir  : ${CACHE_DIR}"
echo "Binary name: ${BINARY_NAME}"
echo ""

# ── Prerequisites ─────────────────────────────────────────────────────────────
for cmd in git cmake make; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "ERROR: '${cmd}' is not installed." >&2
        exit 1
    fi
done

# ── Clone or update source ────────────────────────────────────────────────────
mkdir -p "${CACHE_DIR}"

if [ -d "${WHISPER_SRC}/.git" ]; then
    echo "→ Updating existing whisper.cpp clone..."
    git -C "${WHISPER_SRC}" pull --ff-only
else
    echo "→ Cloning whisper.cpp (shallow)..."
    git clone --depth=1 "${WHISPER_REPO}" "${WHISPER_SRC}"
fi

# ── Build ─────────────────────────────────────────────────────────────────────
echo ""
echo "→ Configuring CMake..."
cmake -B "${WHISPER_SRC}/build" \
    -S "${WHISPER_SRC}" \
    -DWHISPER_BUILD_TESTS=OFF \
    -DWHISPER_BUILD_EXAMPLES=ON \
    -DCMAKE_BUILD_TYPE=Release

echo ""
echo "→ Building (using $(nproc) cores)..."
cmake --build "${WHISPER_SRC}/build" \
    --config Release \
    -j "$(nproc)" \
    --target whisper-cli

# ── Locate built binary ───────────────────────────────────────────────────────
# whisper.cpp >= 1.7.x produces 'whisper-cli'; older builds produced 'main'.
BUILT_BINARY=""
for candidate in \
    "${WHISPER_SRC}/build/bin/whisper-cli" \
    "${WHISPER_SRC}/build/bin/main"; do
    if [ -f "${candidate}" ]; then
        BUILT_BINARY="${candidate}"
        break
    fi
done

if [ -z "${BUILT_BINARY}" ]; then
    echo "ERROR: Could not locate built binary in ${WHISPER_SRC}/build/bin/" >&2
    echo "       Contents of build/bin:" >&2
    ls -la "${WHISPER_SRC}/build/bin/" >&2 || true
    exit 1
fi

echo "→ Found built binary: ${BUILT_BINARY}"

# ── Stage binary ──────────────────────────────────────────────────────────────
mkdir -p "${OUTPUT_DIR}"

DEST="${OUTPUT_DIR}/${BINARY_NAME}"
cp "${BUILT_BINARY}" "${DEST}"
chmod +x "${DEST}"

echo ""
echo "✓ Binary staged: ${DEST}"
echo "  Size: $(du -h "${DEST}" | cut -f1)"
echo ""
echo "=== Build complete ==="