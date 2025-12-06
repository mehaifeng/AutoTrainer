# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AutoTrainer is a .NET 8.0 desktop application built with Avalonia UI that serves as a graphical user interface for training image classification and object detection models using PyTorch. The application manages the entire machine learning workflow, from environment setup and data annotation to model training, validation, and export.

**Key Technologies:**
- **.NET 8.0** with Avalonia UI for the desktop interface
- **Python** with PyTorch and torchvision for machine learning backend
- **CliWrap** (v3.10.0) for cross-platform command execution and process management
- **CommunityToolkit.Mvvm** (v8.4.0) for MVVM pattern implementation
- **Serilog** for structured logging
- **LiveCharts.SkiaSharpView** for data visualization
- **SixLabors.ImageSharp** and **SkiaSharp** for image processing
- **Newtonsoft.Json** for JSON configuration handling
- **Cross-platform support** (Windows, Linux, macOS)

## Architecture

The project follows a hybrid architecture combining a .NET frontend with a Python backend:

### Frontend (.NET/Avalonia)
- **Views/**: Avalonia XAML UI files organized by user controls
- **ViewModels/**: MVVM view models inheriting from ViewModelBase
- **Models/**: Data models and configuration classes
- **Helpers/**: Utility classes including CliWrapHelper for Python execution
- **Converters/**: XAML value converters
- **Extension/**: Extension methods and utilities

### Backend (Python)
- **PyScripts/**: Python scripts for ML operations
  - `Training/`: Training modules organized by task type
    - `Classification/`: Image classification training scripts
      - `base_classifier.py`: Base class for classification trainers
      - Model-specific trainers (efficientnet, mobilenet, etc.)
    - `Detection/`: Object detection training scripts
      - `base_detector.py`: Base class for detection trainers
      - `fasterrcnn_trainer.py`: Faster R-CNN trainer
    - `common/utils.py`: Shared utilities including ModelCheckpoint
  - `Inference/`: Model validation and inference scripts
    - `Classification/`: Classification validators with base class architecture
    - `Detection/`: Detection validators with base class architecture
  - `ModelConverter.py`: Model format conversion (ONNX, TensorFlow)
  - `Utils/`: Utility scripts including ModelMetadataReader

### Communication Bridge
- **CliWrapHelper.cs**: Manages Python virtual environment activation and script execution using CliWrap library
- **ModelParam.json**: Configuration file that passes parameters from .NET to Python
- **JSON logging**: Structured logging system for real-time training progress monitoring

## Build and Development

### Build Commands
```bash
# Build the solution
dotnet build AutoTrainer.sln

# Run the application
dotnet run --project AutoTrainer.csproj

# Build in release mode
dotnet build -c Release
```

### Project Structure
```
AutoTrainer/
├── Views/              # Avalonia XAML UI files
├── ViewModels/         # MVVM view models
├── Models/             # Data models
├── Helpers/            # Utility classes (CliWrapHelper.cs)
├── PyScripts/          # Python ML scripts
├── Configs/            # Configuration files
├── DataSet/            # Training data storage
├── Models/             # Trained model output
├── Logs/               # Application logs
└── Assets/             # UI resources
```

## Key Components

### Configuration System
- **ModelParam.json**: Main configuration template with training parameters
- **Requirements.json**: Python dependencies specification (not YAML)
- **AvailableModels.json**: List of available pre-trained models
- **Dynamic configuration**: .NET generates runtime JSON configs consumed by Python scripts

### Virtual Environment Management
- **Automatic venv creation**: Application can create and manage Python virtual environments
- **Package verification**: Validates required ML packages are installed
- **Cross-platform support**: Handles Windows (.bat), Linux/macOS (bash) activation scripts

### Data Annotation System
- **Image classification**: Folder-based classification (DataSet/ClassifiedImages)
- **Object detection**: COCO format annotation support
- **Visual annotation tools**: Built-in image annotation interface
- **Alternative approach**: Configuration file-based annotation support discussed in docs

### Training Pipeline
1. **Environment setup**: Python venv creation and package installation
2. **Data preparation**: Image loading and preprocessing
3. **Model configuration**: Select from torchvision models
   - Classification: EfficientNet, MobileNetV3, ResNet, VGG, DenseNet, etc.
   - Detection: Faster R-CNN with various backbones (ResNet, MobileNetV3)
4. **Training execution**: Real-time progress monitoring with JSON logging
5. **Validation**: Performance evaluation with detailed metrics and visualization
6. **Export**: Model conversion to ONNX/TensorFlow formats

## Development Workflow

### Python Script Integration
When modifying Python scripts in PyScripts/:
1. Scripts receive configuration via `--config` parameter pointing to JSON file
2. Use the StructuredLogger class for JSON logging that .NET can parse in real-time
3. Follow the existing base class architecture in Training/ and Inference/ directories
4. Ensure cross-platform compatibility (path handling with os.path.join)
5. Model metadata is saved with checkpoints (model_name, num_classes, epoch, metrics)

### Adding New Models
To add new PyTorch models:
1. **Classification models**: Create new trainer inheriting from `base_classifier.py`
2. **Detection models**: Create new trainer inheriting from `base_detector.py`
3. **Validation scripts**: Create corresponding validators in Inference/ directories
4. Update model configurations in both Python and .NET UI
5. Ensure metadata includes standard model architecture names for validation

### Configuration Changes
When modifying ModelParam.json or adding new parameters:
1. Update both the JSON template in Configs/ and any .NET code that generates runtime configs
2. Ensure Python scripts have default values for new parameters
3. Update validation logic in both frontend and backend

## Testing and Validation

### Manual Testing Workflow
1. **Environment test**: Use built-in Python environment detection
2. **Small dataset test**: Verify training pipeline with minimal data
3. **Model validation**: Use ValidModelPerformanceView for performance testing
4. **Export testing**: Validate ONNX/TensorFlow conversion

### Common Debugging Areas
- **Python execution**: Check CliWrapHelper logs for venv activation issues
- **Training progress**: Monitor real-time JSON output (no longer saved to files)
- **Model loading**: Verify paths and permissions for model files
- **Data loading**: Ensure image folders follow expected structure
- **NaN Loss**: Refer to `docs/NaN_Loss_Troubleshooting.md` for gradient explosion solutions

## Cross-Platform Considerations

### Path Handling
- Use Path.Combine() instead of string concatenation
- Respect platform-specific separators (App.Separator property)
- Handle case sensitivity for non-Windows platforms

### Shell Execution
- CliWrapHelper automatically detects OS and uses appropriate shell
- Uses CliWrap library for improved cross-platform compatibility and error handling
- Environment variables set for UTF-8 encoding in Python processes
- Virtual environment activation handles .bat vs bash script differences
- Real-time output streaming with async/await pattern
- Cancellation token support for long-running operations

## Logging and Monitoring

### Application Logs (.NET)
- **Serilog**: Structured logging to Logs/AppLogs/AutoTrainer.log
- **Daily rotation**: Automatic log file rotation by date

### Training Logs (Python)
- **Real-time JSON output**: Training metrics streamed to .NET UI (not saved to files)
- **Structured data**: Epoch-wise loss, accuracy, learning rate tracking
- **Error capture**: Full traceback logging for debugging
- **Progress tracking**: Real-time progress bar updates in UI

## Performance Considerations

### Memory Management
- Image loading and preprocessing optimized for batch processing
- GPU memory usage monitoring in training scripts
- Progressive image loading for large datasets

### Async Operations
- Python script execution is async to prevent UI blocking
- Progress reporting through delegate callbacks
- Cancellation token support for long-running operations

## Current Development Status

### Active Branch: `develop_UIRemaster`
This branch focuses on UI improvements and refactoring of the training architecture.

### Recent Major Changes
1. **Python Script Architecture Refactoring**
   - Modular training scripts organized by task type (Classification/Detection)
   - Base class architecture for trainers and validators
   - Improved model metadata handling with ModelCheckpoint

2. **Model Validation Improvements**
   - Validators read model metadata (model_name, num_classes) for accurate architecture matching
   - COCO annotations now optional for detection validation
   - Classification validation shows confidence scores

3. **NaN Loss Handling**
   - Gradient clipping (max_norm=1.0) implemented in detection training
   - Automatic batch skipping for anomalous loss values
   - Comprehensive troubleshooting guide in `docs/NaN_Loss_Troubleshooting.md`

4. **Training Log Optimization**
   - Removed log file creation - training metrics now streamed directly to UI
   - Real-time progress bar updates fixed
   - Reduced disk I/O and improved performance

### Known Issues & Solutions
- **Gradient Explosion in Detection**: Addressed with gradient clipping and learning rate adjustments
- **Model Architecture Mismatch**: Fixed through metadata-based validation
- **Progress Bar Not Updating**: Resolved by updating EpochState in progress/metrics handlers

## Important Files for Claude

### Core Integration Files
- `Helpers/CliWrapHelper.cs`: Bridge between .NET and Python - manages venv activation and script execution
- `ViewModels/TrainingViewModel.cs`: Handles training progress, JSON log parsing, and UI updates
- `App.axaml.cs`: Application initialization and path configurations

### Configuration Templates
- `Configs/ModelParam.json`: Template for training parameters (DO NOT modify directly)
- `Configs/Requirements.json`: Python package dependencies
- `Configs/AvailableModels.json`: UI model selection options

### Python Base Classes (Do not modify unless adding new models)
- `PyScripts/Training/Classification/base_classifier.py`: Base class for classification trainers
- `PyScripts/Training/Detection/base_detector.py`: Base class for detection trainers
- `PyScripts/Training/common/utils.py`: Shared utilities including ModelCheckpoint class
- `PyScripts/Inference/Classification/base_classifier_validator.py`: Base class for classification validators
- `PyScripts/Inference/Detection/base_detector_validator.py`: Base class for detection validators

### Documentation
- `docs/README.md`: Chinese documentation with UI examples and future roadmap
- `docs/NaN_Loss_Troubleshooting.md`: Comprehensive guide for gradient explosion issues
- `copilot_edit_history.md`: Detailed history of recent changes and architectural decisions