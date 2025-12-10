#!/bin/bash
# Build whisper.cpp with automatic GPU detection and Jellyfin FFmpeg integration
# This script is called by the plugin to build whisper.cpp with optimal settings
# Handles both root and non-root environments

INSTALL_DIR="${1:-/usr/local/bin}"
CACHE_DIR="${2:-$HOME/.cache/whisper-cpp}"
WHISPER_VERSION="v1.8.2"
BUILD_DIR="${CACHE_DIR}/build-temp-$$"
QUIET_MODE=1  # Reduce output verbosity

echo "=================================================="
echo "Whisper.cpp Automatic Build Script"
echo "Install Dir: $INSTALL_DIR"
echo "Cache Dir: $CACHE_DIR"
echo "=================================================="

# Detect GPU type
detect_gpu() {
    # Check for NVIDIA GPU
    if command -v nvidia-smi &> /dev/null; then
        if nvidia-smi &> /dev/null 2>&1; then
            echo "cuda"
            return
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

# Install build dependencies (with permission handling)
install_dependencies() {
    echo "Installing build dependencies..."
    
    # Try installation methods, gracefully skip if permission denied
    if command -v apt-get &> /dev/null; then
        # Try without sudo first
        if apt-get install -y git build-essential cmake pkg-config wget 2>/dev/null; then
            return 0
        fi
        
        # Try with sudo
        if sudo apt-get update -qq 2>/dev/null && \
           sudo apt-get install -y -qq git build-essential cmake pkg-config wget > /dev/null 2>&1; then
            return 0
        fi
        
        # If permissions fail, inform user
        echo "Warning: Could not install dependencies. Proceeding anyway - build may fail if dependencies missing."
        return 0
        
    elif command -v yum &> /dev/null; then
        if yum install -y git gcc gcc-c++ make cmake pkg-config wget 2>/dev/null; then
            return 0
        fi
        if sudo yum install -y git gcc gcc-c++ make cmake pkg-config wget 2>/dev/null; then
            return 0
        fi
        echo "Warning: Could not install dependencies (yum)"
        return 0
        
    elif command -v apk &> /dev/null; then
        if apk add --no-cache git build-base cmake pkgconfig wget 2>/dev/null; then
            return 0
        fi
        if sudo apk add --no-cache git build-base cmake pkgconfig wget 2>/dev/null; then
            return 0
        fi
        echo "Warning: Could not install dependencies (apk)"
        return 0
    fi
    
    echo "Warning: Unknown package manager, dependencies may be missing"
    return 0
}

# Build whisper.cpp
build_whisper() {
    local gpu_type="$1"
    local cmake_flags=""
    
    echo "Building whisper.cpp for: $gpu_type"
    
    # Create build directory
    mkdir -p "$BUILD_DIR"
    
    # Clone repository
    echo "Cloning repository..."
    if ! git clone --depth 1 --branch "$WHISPER_VERSION" https://github.com/ggerganov/whisper.cpp.git "$BUILD_DIR" 2>&1 | grep -v "Cloning\|Resolving\|Unpacking"; then
        echo "Error: Failed to clone whisper.cpp repository"
        return 1
    fi
    
    cd "$BUILD_DIR" || return 1
    
    # Configure build flags based on GPU type
    case "$gpu_type" in
        cuda)
            echo "Configuring for NVIDIA CUDA..."
            cmake_flags="-DGGML_CUDA=1"
            
            # Try to detect GPU compute capability
            if command -v nvidia-smi &> /dev/null; then
                COMPUTE_CAP=$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader 2>/dev/null | head -n1 | tr -d '.')
                if [ -n "$COMPUTE_CAP" ]; then
                    echo "Detected compute capability: $COMPUTE_CAP"
                    if [ "$COMPUTE_CAP" -ge "75" ]; then
                        cmake_flags="$cmake_flags -DCMAKE_CUDA_ARCHITECTURES=75;80;86;89;90"
                    fi
                fi
            fi
            ;;
            
        vulkan)
            echo "Configuring for Vulkan (AMD/Intel)..."
            cmake_flags="-DGGML_VULKAN=1"
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
    
    # Build
    echo "Running cmake..."
    if ! cmake -B build $cmake_flags -DCMAKE_BUILD_TYPE=Release > /dev/null 2>&1; then
        echo "Error: CMake configuration failed"
        return 1
    fi
    
    echo "Compiling (this may take several minutes)..."
    local ncores=$(nproc 2>/dev/null || echo 4)
    if ! cmake --build build -j"$ncores" --config Release 2>&1 | tail -5; then
        echo "Error: Build failed"
        return 1
    fi
    
    # Check if binary was created
    if [ ! -f "build/bin/main" ]; then
        # Try alternative path
        if [ ! -f "build/main" ]; then
            echo "Error: Binary not found after build"
            return 1
        fi
    fi
    
    echo "Build completed successfully"
    return 0
}

# Install binary to target location
install_binary() {
    local binary_source="$BUILD_DIR/build/bin/main"
    if [ ! -f "$binary_source" ]; then
        binary_source="$BUILD_DIR/build/main"
    fi
    
    if [ ! -f "$binary_source" ]; then
        echo "Error: Binary source not found"
        return 1
    fi
    
    echo "Installing binary..."
    
    # Try to install to requested directory
    if mkdir -p "$INSTALL_DIR" 2>/dev/null; then
        if cp "$binary_source" "$INSTALL_DIR/whisper" 2>/dev/null; then
            chmod +x "$INSTALL_DIR/whisper" 2>/dev/null || true
            echo "Binary installed: $INSTALL_DIR/whisper"
            return 0
        fi
    fi
    
    # Try with sudo
    if sudo mkdir -p "$INSTALL_DIR" 2>/dev/null && \
       sudo cp "$binary_source" "$INSTALL_DIR/whisper" 2>/dev/null; then
        sudo chmod +x "$INSTALL_DIR/whisper" 2>/dev/null || true
        echo "Binary installed (with sudo): $INSTALL_DIR/whisper"
        return 0
    fi
    
    # Fallback to cache directory
    echo "Warning: Could not install to $INSTALL_DIR, installing to cache instead"
    cp "$binary_source" "$CACHE_DIR/whisper" 2>/dev/null || return 1
    chmod +x "$CACHE_DIR/whisper" 2>/dev/null || true
    echo "Binary installed to: $CACHE_DIR/whisper"
    echo "Add $CACHE_DIR to your PATH or set WHISPER_CPP_MAIN=$CACHE_DIR/whisper"
    
    return 0
}

# Test binary
test_binary() {
    local binary_path="$1"
    
    echo "Testing binary..."
    if "$binary_path" --help > /dev/null 2>&1; then
        echo "✓ Binary works!"
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
    if ! build_whisper "$GPU_TYPE"; then
        echo "Build failed!"
        return 1
    fi
    
    # Install binary
    if ! install_binary; then
        echo "Installation failed!"
        return 1
    fi
    
    # Test
    test_binary "$INSTALL_DIR/whisper" || test_binary "$CACHE_DIR/whisper"
    
    # Cleanup
    echo "Cleaning up..."
    rm -rf "$BUILD_DIR"
    
    echo ""
    echo "=================================================="
    echo "Installation Complete!"
    echo "Binary: $INSTALL_DIR/whisper or $CACHE_DIR/whisper"
    echo "GPU Type: $GPU_TYPE"
    echo "=================================================="
    return 0
}

# Run main function and exit with its status
main
exit $?