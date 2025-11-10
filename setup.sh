#!/bin/bash

# Setup Script
# This script sets up the Python environment and installs all dependencies

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print functions
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo ""
    echo "==========================================="
    echo "$1"
    echo "==========================================="
    echo ""
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Main setup
main() {
    print_header "Whisper Subtitle Generator - Setup"

    # Check Python installation
    print_info "Checking Python installation..."
    if ! command_exists python3; then
        print_error "Python 3 is not installed. Please install Python 3.8 or higher."
        exit 1
    fi

    PYTHON_VERSION=$(python3 --version | cut -d' ' -f2)
    print_success "Found Python $PYTHON_VERSION"

    # Check if Python version is 3.8 or higher
    PYTHON_MAJOR=$(echo $PYTHON_VERSION | cut -d'.' -f1)
    PYTHON_MINOR=$(echo $PYTHON_VERSION | cut -d'.' -f2)
    
    if [ "$PYTHON_MAJOR" -lt 3 ] || ([ "$PYTHON_MAJOR" -eq 3 ] && [ "$PYTHON_MINOR" -lt 8 ]); then
        print_error "Python 3.8 or higher is required. You have Python $PYTHON_VERSION"
        exit 1
    fi

    # Check FFmpeg installation
    print_info "Checking FFmpeg installation..."
    if ! command_exists ffmpeg; then
        print_warning "FFmpeg is not installed. It is required for Whisper to extract audio from videos."
        echo ""
        echo "Install FFmpeg:"
        echo "  Ubuntu/Debian: sudo apt install ffmpeg"
        echo "  Arch Linux:    sudo pacman -S ffmpeg"
        echo "  macOS:         brew install ffmpeg"
        echo ""
        read -p "Continue without FFmpeg? (y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    else
        FFMPEG_VERSION=$(ffmpeg -version | head -n1 | cut -d' ' -f3)
        print_success "Found FFmpeg $FFMPEG_VERSION"
    fi

    # Create virtual environment
    print_info "Creating Python virtual environment..."
    if [ -d ".venv" ]; then
        print_warning "Virtual environment already exists. Skipping creation."
    else
        python3 -m venv .venv
        print_success "Virtual environment created"
    fi

    # Activate virtual environment
    print_info "Activating virtual environment..."
    source .venv/bin/activate

    # Upgrade pip
    print_info "Upgrading pip..."
    pip install --upgrade pip --quiet

    # Check for NVIDIA GPU
    print_info "Checking for NVIDIA GPU..."
    if command_exists nvidia-smi; then
        GPU_INFO=$(nvidia-smi --query-gpu=name --format=csv,noheader 2>/dev/null | head -n1)
        if [ -n "$GPU_INFO" ]; then
            print_success "Found GPU: $GPU_INFO"
            CUDA_VERSION=$(nvidia-smi | grep "CUDA Version" | awk '{print $9}')
            if [ -n "$CUDA_VERSION" ]; then
                print_success "CUDA Version: $CUDA_VERSION"
            fi
            USE_GPU=true
        else
            print_warning "NVIDIA GPU detected but not accessible"
            USE_GPU=false
        fi
    else
        print_warning "No NVIDIA GPU detected. CPU processing will be slower."
        USE_GPU=false
    fi

    # Install PyTorch
    print_info "Installing PyTorch..."
    if [ "$USE_GPU" = true ]; then
        print_info "Installing PyTorch with CUDA support..."
        pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
    else
        print_info "Installing PyTorch (CPU-only version)..."
        pip install torch torchvision torchaudio
    fi
    print_success "PyTorch installed"

    # Install Whisper and other dependencies
    print_info "Installing OpenAI Whisper and dependencies..."
    pip install -U openai-whisper tqdm
    print_success "All dependencies installed"

    # Verify installation
    print_info "Verifying installation..."
    python3 << 'EOF'
import sys
try:
    import whisper
    import torch
    import tqdm
    
    print(f"✓ Whisper version: {whisper.__version__}")
    print(f"✓ PyTorch version: {torch.__version__}")
    print(f"✓ CUDA available: {torch.cuda.is_available()}")
    
    if torch.cuda.is_available():
        print(f"✓ GPU: {torch.cuda.get_device_name(0)}")
    
    sys.exit(0)
except ImportError as e:
    print(f"✗ Import error: {e}")
    sys.exit(1)
EOF

    if [ $? -eq 0 ]; then
        print_success "Installation verified successfully"
    else
        print_error "Installation verification failed"
        exit 1
    fi

    # Make batch_generate.py executable
    if [ -f "batch_generate.py" ]; then
        chmod +x batch_generate.py
        print_success "Made batch_generate.py executable"
    fi

    # Setup complete
    print_header "Setup Complete!"
    
    echo "To get started:"
    echo ""
    echo "  1. Activate the virtual environment:"
    echo -e "     ${GREEN}source .venv/bin/activate${NC}"
    echo ""
    echo "  2. Configure your media folders in batch_generate.py"
    echo ""
    echo "  3. Run the script:"
    echo -e "     ${GREEN}python batch_generate.py${NC}"
    echo ""
    echo "  4. Or process a specific folder:"
    echo -e "     ${GREEN}python batch_generate.py /path/to/media${NC}"
    echo ""
    echo "For more options, run:"
    echo -e "  ${GREEN}python batch_generate.py --help${NC}"
    echo ""
    
    if [ "$USE_GPU" = false ]; then
        print_warning "Note: Running without GPU. Processing will be significantly slower."
        echo "Consider using a smaller model (--model tiny or --model base) for faster processing."
        echo ""
    fi
}

# Run main function
main

