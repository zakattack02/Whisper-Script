#!/bin/bash
# Build-whisper.sh
# Builds whisper.cpp from source and stages the binary for plugin use.
#
# Usage:
#   bash Build-whisper.sh [--no-docker] <OUTPUT_DIR> [CACHE_DIR]
#
#   --no-docker : Force native build (skip Docker even if available)
#   OUTPUT_DIR : Where the final binary lands (e.g. plugin's whisper/linux-x64/)
#   CACHE_DIR  : Where to clone/cache the whisper.cpp source (default: /tmp/whisper-cache)

set -e

# ── Parse arguments ────────────────────────────────────────────────
USE_DOCKER=true  # Docker is now the default
FORCE_NATIVE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --no-docker)
            FORCE_NATIVE=true
            shift
            ;;
        *)
            break
            ;;
    esac
done

OUTPUT_DIR="${1:?ERROR: OUTPUT_DIR not specified. Usage: Build-whisper.sh [--no-docker] <OUTPUT_DIR> [CACHE_DIR]}"
CACHE_DIR="${2:-/tmp/whisper-cache}"

# The name the plugin expects on disk inside its own directory.
# WhisperBinaryManager.FindBundledBinary() looks for this exact name.
BINARY_NAME="whisper-whisper-cli"

WHISPER_REPO="https://github.com/ggerganov/whisper.cpp"
WHISPER_SRC="${CACHE_DIR}/whisper.cpp"

# ── Docker build function ──────────────────────────────────────────
build_in_docker() {
    local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local dockerfile="${script_dir}/Dockerfile.whisper"
    
    if [ ! -f "$dockerfile" ]; then
        echo "ERROR: Dockerfile not found at $dockerfile" >&2
        exit 1
    fi
    
    if ! command -v docker &>/dev/null; then
        echo "ERROR: Docker is not installed but Docker build was attempted." >&2
        echo "       Use --no-docker flag for native build, or install Docker." >&2
        exit 1
    fi
    
    # Check if Docker daemon is running
    if ! docker info &>/dev/null; then
        echo "ERROR: Docker daemon is not running." >&2
        echo "       Start Docker with: sudo systemctl start docker" >&2
        echo "       Or use --no-docker flag for native build." >&2
        exit 1
    fi
    
    echo "=== Building whisper.cpp in Docker (Ubuntu 22.04) ==="
    echo "Output dir : ${OUTPUT_DIR}"
    echo "Dockerfile : ${dockerfile}"
    echo ""
    
    # Build image
    echo "→ Building Docker image..."
    docker build -t whisper-builder -f "$dockerfile" .
    
    # Run container with output directory mounted
    echo "→ Extracting binary from container..."
    mkdir -p "$OUTPUT_DIR"
    
    # Container CMD copies CPU binary, CUDA binary, and CUDA .so files
    docker run --rm -v "${OUTPUT_DIR}:/output" whisper-builder
    
    # Docker creates files as root. Try to fix ownership if possible.
    if command -v sudo &>/dev/null; then
        sudo chown -R "$(id -u):$(id -g)" "$OUTPUT_DIR" 2>/dev/null || true
    fi
    
    # Verify CPU binary
    local cpu_dest="${OUTPUT_DIR}/${BINARY_NAME}"
    if [ ! -f "$cpu_dest" ]; then
        echo "ERROR: CPU binary not found after Docker build at $cpu_dest" >&2
        exit 1
    fi
    
    chmod +x "$cpu_dest" 2>/dev/null || true
    echo ""
    echo "✓ CPU binary: ${cpu_dest} ($(du -h "${cpu_dest}" | cut -f1))"
    
    # Verify CUDA binary (optional — only produced when the CUDA stage builds)
    local cuda_dest="${OUTPUT_DIR}/${BINARY_NAME}-cuda"
    if [ -f "$cuda_dest" ]; then
        chmod +x "$cuda_dest" 2>/dev/null || true
        echo "✓ CUDA binary: ${cuda_dest} ($(du -h "${cuda_dest}" | cut -f1))"
        
        # Verify CUDA .so files
        for lib in libcudart.so.12 libcublas.so.12 libcublasLt.so.12 libnccl.so.2 libnccl.so; do
            if [ -f "${OUTPUT_DIR}/${lib}" ]; then
                chmod +x "${OUTPUT_DIR}/${lib}" 2>/dev/null || true
                echo "  ✓ ${lib} ($(du -h "${OUTPUT_DIR}/${lib}" | cut -f1))"
            else
                echo "  ⚠ ${lib} missing — CUDA binary may not function"
            fi
        done
    else
        echo "  (CUDA binary not built — GPU support unavailable)"
    fi
    
    # Verify GLIBC dependencies on CPU binary
    echo ""
    echo "→ Checking GLIBC requirements..."
    if command -v objdump &>/dev/null; then
        local glibc_reqs=$(objdump -T "$cpu_dest" 2>/dev/null | grep GLIBC_ | grep -oP 'GLIBC_[0-9.]+' | sort -V | uniq | tail -1)
        echo "  CPU binary highest GLIBC requirement: ${glibc_reqs:-unknown}"
        
        if [ -f "$cuda_dest" ]; then
            local cuda_glibc=$(objdump -T "$cuda_dest" 2>/dev/null | grep GLIBC_ | grep -oP 'GLIBC_[0-9.]+' | sort -V | uniq | tail -1)
            echo "  CUDA binary highest GLIBC requirement: ${cuda_glibc:-unknown}"
        fi
        
        # Warn if GLIBC_2.43 is still required
        if echo "$glibc_reqs" | grep -q "2.43"; then
            echo "  WARNING: CPU binary still requires GLIBC 2.43! Docker build may have used wrong base image."
        fi
    fi
    
    echo ""
    echo "=== Docker build complete ==="
    exit 0
}

# ── Determine build method ─────────────────────────────────────────
if [ "$FORCE_NATIVE" = false ] && command -v docker &>/dev/null; then
    # Docker is available and not disabled, use it
    if docker info &>/dev/null; then
        build_in_docker
    else
        # Docker is installed but daemon is not running or permission issue
        echo "ERROR: Docker is installed but daemon is not running or permission denied." >&2
        echo "       To start Docker: sudo systemctl start docker" >&2
        echo "       To add user to docker group: sudo usermod -aG docker \$USER" >&2
        echo "       Then logout/login or run: newgrp docker" >&2
        echo "" >&2
        echo "       Or use --no-docker flag for native build (not recommended for releases)." >&2
        exit 1
    fi
elif [ "$FORCE_NATIVE" = true ]; then
    echo "→ Native build forced with --no-docker flag"
    echo "  WARNING: Binary may require GLIBC 2.43+ and may not work on older systems."
    echo ""
else
    echo "→ Docker not available, using native build"
    echo "  WARNING: Binary may require GLIBC 2.43+ and may not work on older systems."
    echo ""
fi

# ── Native build (fallback or forced) ─────────────────────────────
echo "=== whisper.cpp Build Script (Native) ==="
echo "Output dir : ${OUTPUT_DIR}"
echo "Cache dir  : ${CACHE_DIR}"
echo "Binary name: ${BINARY_NAME}"
echo ""

# ── Prerequisites ─────────────────────────────────────────────────────
for cmd in git cmake make; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "ERROR: '${cmd}' is not installed." >&2
        exit 1
    fi
done

# ── Clone or update source ────────────────────────────────────────────
mkdir -p "${CACHE_DIR}"

if [ -d "${WHISPER_SRC}/.git" ]; then
    echo "→ Updating existing whisper.cpp clone..."
    git -C "${WHISPER_SRC}" pull --ff-only
else
    echo "→ Cloning whisper.cpp (shallow)..."
    git clone --depth=1 "${WHISPER_REPO}" "${WHISPER_SRC}"
fi

# ── Build ─────────────────────────────────────────────────────────────
echo ""
echo "→ Configuring CMake..."
cmake -B "${WHISPER_SRC}/build" \
    -S "${WHISPER_SRC}" \
    -DWHISPER_BUILD_TESTS=OFF \
    -DWHISPER_BUILD_EXAMPLES=ON \
    -DBUILD_SHARED_LIBS=OFF \
    -DGGML_OPENMP=OFF \
    -DGGML_NATIVE=OFF \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_C_FLAGS="-march=x86-64 -mtune=generic" \
    -DCMAKE_CXX_FLAGS="-march=x86-64 -mtune=generic" \
    -DCMAKE_EXE_LINKER_FLAGS="-static-libgcc -static-libstdc++ -Wl,--as-needed"

echo ""
echo "→ Building (using $(nproc) cores)..."
cmake --build "${WHISPER_SRC}/build" \
    --config Release \
    -j "$(nproc)" \
    --target whisper-cli

# ── Locate built binary ───────────────────────────────────────────────
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

# ── Stage binary ──────────────────────────────────────────────────────
mkdir -p "${OUTPUT_DIR}"

DEST="${OUTPUT_DIR}/${BINARY_NAME}"
cp "${BUILT_BINARY}" "${DEST}"
chmod +x "${DEST}"

echo ""
echo "✓ Binary staged: ${DEST}"
echo "  Size: $(du -h "${DEST}" | cut -f1)"

# Verify GLIBC dependencies
echo ""
echo "→ Checking GLIBC requirements..."
if command -v objdump &>/dev/null; then
    glibc_reqs=$(objdump -T "$DEST" 2>/dev/null | grep GLIBC_ | grep -oP 'GLIBC_[0-9.]+' | sort -V | uniq | tail -1)
    echo "  Highest GLIBC requirement: ${glibc_reqs:-unknown}"
    
    # Warn if GLIBC_2.43 is required
    if echo "$glibc_reqs" | grep -q "2.43"; then
        echo "  WARNING: Binary requires GLIBC 2.43. Consider using Docker build for compatibility."
    fi
fi

echo ""
echo "=== Build complete ==="
