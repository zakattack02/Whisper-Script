#!/bin/bash
# Build whisper.cpp with automatic GPU detection and Jellyfin FFmpeg integration
# This script is called by the plugin to build whisper.cpp with optimal settings

set -e

INSTALL_DIR="${1:-/usr/local/bin}"
CACHE_DIR="${2:-$HOME/.cache/whisper-cpp}"
WHISPER_VERSION="v1.7.1"
BUILD_DIR="/tmp/whisper-cpp-build-$$"

echo "=================================================="
echo "Whisper.cpp Automatic Build Script"
echo "Install Dir: $INSTALL_DIR"
echo "Cache Dir: $CACHE_DIR"
echo "=================================================="

# Detect GPU type
detect_gpu() {
    # Check for NVIDIA GPU
    if command -v nvidia-smi &> /dev/null; then
        if nvidia-smi &> /dev/null; then
            GPU_NAME=$(nvidia-smi --query-gpu=name --format=csv,noheader 2>/dev/null | head -n1)
            if [ -n "$GPU_NAME" ]; then
                echo "cuda"
                return
            fi
        fi
    fi
    
    # Check for Vulkan (AMD/Intel)
    if command -v vulkaninfo &> /dev/null; then
        if vulkaninfo --summary 2>&1 | grep -q "deviceName"; then
            echo "vulkan"
            return
        fi
    fi
    
    # Check for Apple Silicon
    if [[ "$OSTYPE" == "darwin"* ]]; then
        if sysctl -n machdep.cpu.brand_string 2>/dev/null | grep -q "Apple"; then
            echo "metal"
            return
        fi
    fi
    
    echo "cpu"
}

# Install build dependencies
install_dependencies() {
    echo "Installing build dependencies..."
    
    if command -v apt-get &> /dev/null; then
        apt-get update -qq
        apt-get install -y -qq git build-essential cmake pkg-config wget > /dev/null 2>&1
    elif command -v yum &> /dev/null; then
        yum install -y -q git gcc gcc-c++ make cmake pkg-config wget
    elif command -v apk &> /dev/null; then
        apk add --no-cache git build-base cmake pkgconfig wget
    else
        echo "Warning: Unknown package manager, dependencies may be missing"
    fi
}

# Build whisper.cpp
build_whisper() {
    local gpu_type="$1"
    local cmake_flags=""
    
    echo "Building whisper.cpp for: $gpu_type"
    
    # Clone repository
    rm -rf "$BUILD_DIR"
    git clone --depth 1 --branch "$WHISPER_VERSION" https://github.com/ggerganov/whisper.cpp.git "$BUILD_DIR"
    cd "$BUILD_DIR"
    
    # Configure build flags based on GPU type
    case "$gpu_type" in
        cuda)
            echo "Configuring for NVIDIA CUDA..."
            cmake_flags="-DGGML_CUDA=1"
            
            # Detect GPU compute capability
            if command -v nvidia-smi &> /dev/null; then
                COMPUTE_CAP=$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader | head -n1 | tr -d '.')
                if [ -n "$COMPUTE_CAP" ]; then
                    echo "Detected compute capability: $COMPUTE_CAP"
                    # Support common architectures: 7.5 (Turing), 8.0/8.6/8.9 (Ampere/Ada), 9.0 (Hopper)
                    if [ "$COMPUTE_CAP" -ge "75" ]; then
                        cmake_flags="$cmake_flags -DCMAKE_CUDA_ARCHITECTURES=75;80;86;89;90"
                    fi
                fi
            fi
            ;;
            
        vulkan)
            echo "Configuring for Vulkan (AMD/Intel)..."
            cmake_flags="-DGGML_VULKAN=1"
            
            # Install Vulkan SDK if not present
            if ! pkg-config --exists vulkan; then
                echo "Installing Vulkan SDK..."
                if command -v apt-get &> /dev/null; then
                    apt-get install -y -qq libvulkan-dev vulkan-tools > /dev/null 2>&1
                fi
            fi
            ;;
            
        metal)
            echo "Configuring for Apple Metal..."
            cmake_flags="-DGGML_METAL=1"
            ;;
            
        cpu)
            echo "Configuring for CPU only..."
            cmake_flags=""
            ;;
    esac
    
    # Find Jellyfin's FFmpeg
    FFMPEG_PATH=""
    for path in "/usr/lib/jellyfin-ffmpeg/ffmpeg" "/usr/lib/jellyfin-ffmpeg5/ffmpeg" "/usr/lib/jellyfin-ffmpeg6/ffmpeg"; do
        if [ -f "$path" ]; then
            FFMPEG_PATH="$path"
            echo "Found Jellyfin FFmpeg at: $FFMPEG_PATH"
            break
        fi
    done
    
    # Enable FFmpeg if available
    if [ -n "$FFMPEG_PATH" ]; then
        echo "Enabling FFmpeg support..."
        
        # Find FFmpeg libraries
        FFMPEG_LIB_DIR=$(dirname "$FFMPEG_PATH")/../lib
        if [ -d "$FFMPEG_LIB_DIR" ]; then
            export PKG_CONFIG_PATH="$FFMPEG_LIB_DIR/pkgconfig:$PKG_CONFIG_PATH"
        fi
        
        cmake_flags="$cmake_flags -DWHISPER_FFMPEG=ON"
    else
        echo "Jellyfin FFmpeg not found, using default audio handling"
    fi
    
    # Build
    echo "Running cmake with flags: $cmake_flags"
    cmake -B build $cmake_flags -DCMAKE_BUILD_TYPE=Release
    
    echo "Compiling (this may take several minutes)..."
    cmake --build build -j$(nproc 2>/dev/null || echo 4) --config Release
    
    # Install binary
    echo "Installing binary..."
    mkdir -p "$INSTALL_DIR"
    
    local binary_name="whisper"
    if [ "$gpu_type" != "cpu" ]; then
        binary_name="whisper-$gpu_type"
    fi
    
    cp build/bin/main "$INSTALL_DIR/$binary_name"
    chmod +x "$INSTALL_DIR/$binary_name"
    
    # Create symlink for default binary
    ln -sf "$INSTALL_DIR/$binary_name" "$INSTALL_DIR/whisper"
    
    echo "Binary installed: $INSTALL_DIR/$binary_name"
}

# Test binary
test_binary() {
    local binary_path="$1"
    
    echo "Testing binary..."
    if "$binary_path" --help > /dev/null 2>&1; then
        echo "✓ Binary works!"
        
        # Show build info
        "$binary_path" --help 2>&1 | grep -i "cuda\|vulkan\|metal\|ffmpeg" || true
        
        return 0
    else
        echo "✗ Binary test failed!"
        return 1
    fi
}

# Main execution
main() {
    # Detect GPU
    GPU_TYPE=$(detect_gpu)
    echo "Detected GPU type: $GPU_TYPE"
    
    # Install dependencies
    install_dependencies
    
    # Build whisper.cpp
    build_whisper "$GPU_TYPE"
    
    # Test
    test_binary "$INSTALL_DIR/whisper"
    
    # Cleanup
    echo "Cleaning up..."
    rm -rf "$BUILD_DIR"
    
    echo ""
    echo "=================================================="
    echo "Installation Complete!"
    echo "Binary: $INSTALL_DIR/whisper"
    echo "GPU Type: $GPU_TYPE"
    echo "=================================================="
}

# Run main function
main