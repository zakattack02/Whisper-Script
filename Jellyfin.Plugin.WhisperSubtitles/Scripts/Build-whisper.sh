#!/bin/bash
# Whisper.cpp Build Script for Jellyfin Plugin
# Automatically detects GPU and builds with appropriate acceleration

set -e

# Arguments
INSTALL_DIR="${1:-/cache/whisper-cpp}"
CACHE_DIR="${2:-/cache/whisper-cpp}"

echo "=================================================="
echo "Whisper.cpp Automatic Build Script"
echo "Install Dir: $INSTALL_DIR"
echo "Cache Dir: $CACHE_DIR"
echo "=================================================="

# Detect GPU type
detect_gpu() {
    if command -v nvidia-smi &> /dev/null && nvidia-smi &> /dev/null 2>&1; then
        echo "cuda"
    elif command -v vulkaninfo &> /dev/null && vulkaninfo --summary &> /dev/null 2>&1; then
        echo "vulkan"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo "metal"
    else
        echo "cpu"
    fi
}

GPU_TYPE=$(detect_gpu)
echo "Detected GPU type: $GPU_TYPE"

# Install dependencies (best effort, may fail in restricted environments)
install_dependencies() {
    echo "Installing build dependencies..."
    
    if command -v apt-get &> /dev/null; then
        apt-get update -qq 2>/dev/null || true
        apt-get install -y -qq git build-essential cmake 2>/dev/null || true
    elif command -v yum &> /dev/null; then
        yum install -y -q git gcc gcc-c++ make cmake 2>/dev/null || true
    elif command -v apk &> /dev/null; then
        apk add --quiet git build-base cmake 2>/dev/null || true
    fi
    
    # Check if installation succeeded
    if ! command -v git &> /dev/null || ! command -v make &> /dev/null; then
        echo "Warning: Could not install dependencies. Proceeding anyway - build may fail if dependencies missing."
        return 1
    fi
    
    return 0
}

# Try to install dependencies (may fail in restricted environments)
install_dependencies || true

# Create directories
mkdir -p "$CACHE_DIR"
mkdir -p "$INSTALL_DIR"

echo "Building whisper.cpp for: $GPU_TYPE"

# Clone whisper.cpp
REPO_DIR="$CACHE_DIR/whisper.cpp"
if [ ! -d "$REPO_DIR" ]; then
    echo "Cloning repository..."
    if ! git clone https://github.com/ggerganov/whisper.cpp "$REPO_DIR" 2>/dev/null; then
        echo "Error: Failed to clone repository"
        exit 1
    fi
else
    echo "Repository already exists, pulling latest..."
    cd "$REPO_DIR"
    git pull 2>/dev/null || true
fi

cd "$REPO_DIR"

# Build based on GPU type
case $GPU_TYPE in
    cuda)
        echo "Configuring for NVIDIA CUDA..."
        
        # Detect CUDA compute capability
        if command -v nvidia-smi &> /dev/null; then
            COMPUTE_CAP=$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader | head -n1 | tr -d '.')
            echo "Detected compute capability: $COMPUTE_CAP"
        else
            COMPUTE_CAP="75"  # Default to Turing
            echo "Using default compute capability: $COMPUTE_CAP"
        fi
        
        # Try CMake build first
        if command -v cmake &> /dev/null; then
            echo "Running cmake..."
            cmake -B build -DGGML_CUDA=ON -DCUDA_ARCHITECTURES="$COMPUTE_CAP" 2>/dev/null || {
                echo "Error: CMake configuration failed"
                exit 1
            }
            cmake --build build --config Release --target main -j$(nproc) 2>/dev/null || {
                echo "Error: CMake build failed"
                exit 1
            }
            cp build/bin/main "$INSTALL_DIR/main" 2>/dev/null || cp build/main "$INSTALL_DIR/main"
        else
            # Fallback to make
            echo "Running make with CUDA..."
            GGML_CUDA=1 make -j$(nproc) main 2>/dev/null || {
                echo "Error: Make build failed"
                exit 1
            }
            cp main "$INSTALL_DIR/main"
        fi
        ;;
        
    vulkan)
        echo "Configuring for Vulkan..."
        
        if command -v cmake &> /dev/null; then
            cmake -B build -DGGML_VULKAN=ON 2>/dev/null || {
                echo "Error: CMake configuration failed"
                exit 1
            }
            cmake --build build --config Release --target main -j$(nproc) 2>/dev/null || {
                echo "Error: CMake build failed"
                exit 1
            }
            cp build/bin/main "$INSTALL_DIR/main" 2>/dev/null || cp build/main "$INSTALL_DIR/main"
        else
            GGML_VULKAN=1 make -j$(nproc) main 2>/dev/null || {
                echo "Error: Make build failed"
                exit 1
            }
            cp main "$INSTALL_DIR/main"
        fi
        ;;
        
    metal)
        echo "Configuring for Apple Metal..."
        
        if command -v cmake &> /dev/null; then
            cmake -B build -DGGML_METAL=ON 2>/dev/null || {
                echo "Error: CMake configuration failed"
                exit 1
            }
            cmake --build build --config Release --target main -j$(sysctl -n hw.ncpu) 2>/dev/null || {
                echo "Error: CMake build failed"
                exit 1
            }
            cp build/bin/main "$INSTALL_DIR/main" 2>/dev/null || cp build/main "$INSTALL_DIR/main"
        else
            make -j$(sysctl -n hw.ncpu) main 2>/dev/null || {
                echo "Error: Make build failed"
                exit 1
            }
            cp main "$INSTALL_DIR/main"
        fi
        ;;
        
    *)
        echo "Building CPU-only version..."
        
        if command -v cmake &> /dev/null; then
            cmake -B build 2>/dev/null || {
                echo "Error: CMake configuration failed"
                exit 1
            }
            cmake --build build --config Release --target main -j$(nproc 2>/dev/null || echo 2) 2>/dev/null || {
                echo "Error: CMake build failed"
                exit 1
            }
            cp build/bin/main "$INSTALL_DIR/main" 2>/dev/null || cp build/main "$INSTALL_DIR/main"
        else
            make -j$(nproc 2>/dev/null || echo 2) main 2>/dev/null || {
                echo "Error: Make build failed"
                exit 1
            }
            cp main "$INSTALL_DIR/main"
        fi
        ;;
esac

# Make executable
chmod +x "$INSTALL_DIR/main"

# Verify binary exists
if [ ! -f "$INSTALL_DIR/main" ]; then
    echo "Error: Binary not found after build"
    exit 1
fi

echo "=================================================="
echo "Build complete!"
echo "Binary location: $INSTALL_DIR/main"
echo "=================================================="

exit 0