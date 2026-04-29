#!/bin/bash
# Whisper.cpp Build Script for Jellyfin Plugin

set -e

# Arguments - use local paths by default, not /cache (that's the container path)
INSTALL_DIR="${1:-/tmp/whisper-out}"
CACHE_DIR="${2:-/tmp/whisper-cache}"

echo "=================================================="
echo "Whisper.cpp Automatic Build Script"
echo "Install Dir: $INSTALL_DIR"
echo "Cache Dir: $CACHE_DIR"
echo "=================================================="

# Detect GPU type
detect_gpu() {
    if command -v nvidia-smi &>/dev/null && nvidia-smi &>/dev/null 2>&1; then
        echo "cuda"
    elif command -v vulkaninfo &>/dev/null && vulkaninfo --summary &>/dev/null 2>&1; then
        echo "vulkan"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo "metal"
    else
        echo "cpu"
    fi
}

GPU_TYPE=$(detect_gpu)
echo "Detected GPU type: $GPU_TYPE"

# Find CUDA toolkit root — handles non-standard install paths like /opt/cuda
find_cuda_root() {
    # Check common locations
    for candidate in \
        "$(dirname "$(command -v nvcc 2>/dev/null)")/.." \
        /opt/cuda \
        /usr/local/cuda \
        /usr/cuda; do
        if [ -f "$candidate/bin/nvcc" ]; then
            echo "$(realpath "$candidate")"
            return 0
        fi
    done
    return 1
}

# Install dependencies (best effort)
install_dependencies() {
    echo "Installing build dependencies..."
    if command -v apt-get &>/dev/null; then
        apt-get update -qq 2>/dev/null || true
        apt-get install -y -qq git build-essential cmake 2>/dev/null || true
    elif command -v yum &>/dev/null; then
        yum install -y -q git gcc gcc-c++ make cmake 2>/dev/null || true
    elif command -v apk &>/dev/null; then
        apk add --quiet git build-base cmake 2>/dev/null || true
    fi

    if ! command -v git &>/dev/null || ! command -v cmake &>/dev/null; then
        echo "Warning: Could not install dependencies."
        return 1
    fi
    return 0
}

install_dependencies || true

mkdir -p "$CACHE_DIR" "$INSTALL_DIR"

echo "Building whisper.cpp for: $GPU_TYPE"

# Clone or update repo
REPO_DIR="$CACHE_DIR/whisper.cpp"
if [ ! -d "$REPO_DIR" ]; then
    echo "Cloning repository..."
    git clone --depth=1 https://github.com/ggerganov/whisper.cpp "$REPO_DIR"
else
    echo "Repository already exists, pulling latest..."
    git -C "$REPO_DIR" pull || true
fi

cd "$REPO_DIR"

# Helper: copy binary from either cmake output location
copy_binary() {
    for candidate in \
        "build/bin/whisper-cli" \
        "build/bin/main"; do
        if [ -f "$candidate" ]; then
            cp "$candidate" "$INSTALL_DIR/whisper-cli"
            echo "Copied $candidate → $INSTALL_DIR/whisper-cli"
            return 0
        fi
    done
    echo "Error: Binary not found. Built targets:"
    find build/bin/ -type f -executable 2>/dev/null
    exit 1
}

case $GPU_TYPE in
    cuda)
        echo "Configuring for NVIDIA CUDA..."

        COMPUTE_CAP=$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader \
            | head -n1 | tr -d '.')
        echo "Detected compute capability: $COMPUTE_CAP"

        # Find CUDA root so cmake can locate nvcc even under sudo
        CUDA_ROOT=$(find_cuda_root) || {
            echo "Error: Cannot find CUDA toolkit. Is nvcc installed?"
            echo "  Try: export PATH=\$PATH:/opt/cuda/bin and re-run without sudo"
            exit 1
        }
        echo "CUDA root: $CUDA_ROOT"

        cmake -B build \
            -DGGML_CUDA=ON \
            -DCUDA_ARCHITECTURES="$COMPUTE_CAP" \
            -DWHISPER_BUILD_TESTS=OFF \
            -DWHISPER_BUILD_EXAMPLES=ON \
            -DCUDAToolkit_ROOT="$CUDA_ROOT" \
            -DCMAKE_CUDA_COMPILER="$CUDA_ROOT/bin/nvcc" || {
            echo "Error: CMake configuration failed"
            exit 1
        }

        cmake --build build \
            --config Release \
            --target whisper-cli \
            -j"$(nproc)" || {
            echo "Error: CMake build failed"
            exit 1
        }

        copy_binary
        ;;

    vulkan)
        echo "Configuring for Vulkan..."
        cmake -B build \
            -DGGML_VULKAN=ON \
            -DWHISPER_BUILD_TESTS=OFF \
            -DWHISPER_BUILD_EXAMPLES=ON || {
            echo "Error: CMake configuration failed"; exit 1
        }
        cmake --build build --config Release --target whisper-cli -j"$(nproc)" || {
            echo "Error: CMake build failed"; exit 1
        }
        copy_binary
        ;;

    metal)
        echo "Configuring for Apple Metal..."
        cmake -B build \
            -DGGML_METAL=ON \
            -DWHISPER_BUILD_TESTS=OFF \
            -DWHISPER_BUILD_EXAMPLES=ON || {
            echo "Error: CMake configuration failed"; exit 1
        }
        cmake --build build --config Release --target whisper-cli \
            -j"$(sysctl -n hw.ncpu)" || {
            echo "Error: CMake build failed"; exit 1
        }
        copy_binary
        ;;

    *)
        echo "Building CPU-only version..."
        cmake -B build \
            -DWHISPER_BUILD_TESTS=OFF \
            -DWHISPER_BUILD_EXAMPLES=ON \
            -DCMAKE_BUILD_TYPE=Release || {
            echo "Error: CMake configuration failed"; exit 1
        }
        cmake --build build --config Release --target whisper-cli \
            -j"$(nproc 2>/dev/null || echo 2)" || {
            echo "Error: CMake build failed"; exit 1
        }
        copy_binary
        ;;
esac

chmod +x "$INSTALL_DIR/whisper-cli"

[ ! -f "$INSTALL_DIR/main" ] && { echo "Error: Binary not found after build"; exit 1; }

echo "=================================================="
echo "Build complete!"
echo "Binary: $INSTALL_DIR/main"
echo "GPU:    $GPU_TYPE"
echo "=================================================="
exit 0