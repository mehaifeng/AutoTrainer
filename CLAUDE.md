# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AutoTrainer is a .NET 8.0 desktop application built with Avalonia UI that serves as a graphical user interface for training image classification and regression models using PyTorch. The application manages the entire machine learning workflow, from environment setup and data annotation to model training, validation, and export.

**Key Technologies:**
- **.NET 8.0** with Avalonia UI for the desktop interface
- **Python** with PyTorch for machine learning backend
- **MVVM pattern** for UI architecture
- **Serilog** for logging
- **Cross-platform support** (Windows, Linux, macOS)

## Architecture

The project follows a hybrid architecture combining a .NET frontend with a Python backend:

### Frontend (.NET/Avalonia)
- **Views/**: Avalonia XAML UI files organized by user controls
- **ViewModels/**: MVVM view models inheriting from ViewModelBase
- **Models/**: Data models and configuration classes
- **Helpers/**: Utility classes including CmdHelper for Python execution
- **Converters/**: XAML value converters
- **Extension/**: Extension methods and utilities

### Backend (Python)
- **PyScripts/**: Python scripts for ML operations
  - `ModelTrainer.py`: Main training script with configurable model architectures
  - `ImageClassifier.py`: Image classification inference
  - `ModelConverter.py`: Model format conversion (ONNX, TensorFlow)
  - `ModelHelper.py`: Utility functions for model operations

### Communication Bridge
- **CmdHelper.cs**: Manages Python virtual environment activation and script execution
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
├── Helpers/            # Utility classes (CmdHelper.cs)
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
- **Requirements.yaml**: Python dependencies for virtual environments
- **Dynamic configuration**: .NET generates runtime JSON configs consumed by Python scripts

### Virtual Environment Management
- **Automatic venv creation**: Application can create and manage Python virtual environments
- **Package verification**: Validates required ML packages are installed
- **Cross-platform support**: Handles Windows (.bat), Linux/macOS (bash) activation scripts

### Data Annotation System
- **Image classification**: Folder-based classification (DataSet/ClassifiedImages)
- **Alternative approach**: Configuration file-based annotation support discussed in docs
- **Visual annotation tools**: Built-in image annotation interface

### Training Pipeline
1. **Environment setup**: Python venv creation and package installation
2. **Data preparation**: Image loading and preprocessing
3. **Model configuration**: Select from torchvision models (ResNet, VGG, DenseNet, etc.)
4. **Training execution**: Real-time progress monitoring with JSON logging
5. **Validation**: Performance evaluation with detailed metrics
6. **Export**: Model conversion to ONNX/TensorFlow formats

## Development Workflow

### Python Script Integration
When modifying Python scripts in PyScripts/:
1. Scripts receive configuration via `--config` parameter pointing to JSON file
2. Use the TrainingLogger class for structured logging that .NET can parse
3. Follow the existing model configuration patterns in ModelTrainer.py
4. Ensure cross-platform compatibility (path handling with os.path.join)

### Adding New Models
To add new PyTorch models:
1. Update model_input_sizes dictionary in ModelTrainer.py
2. Add classifier modification logic in _modify_classifier method
3. Update .NET UI model selection options
4. Test with both pretrained and local weight loading

### Configuration Changes
When modifying ModelParam.json or adding new parameters:
1. Update both the JSON template in Configs/ and any .NET code that generates runtime configs
2. Ensure Python scripts have default values for new parameters
3. Update validation logic in both frontend and backend

## Testing and Validation

### Manual Testing Workflow
1. **Environment test**: Use built-in Python environment detection
2. **Small dataset test**: Verify training pipeline with minimal data
3. **Model validation**: Use VisualVerifyView for performance testing
4. **Export testing**: Validate ONNX/TensorFlow conversion

### Common Debugging Areas
- **Python execution**: Check CmdHelper logs for venv activation issues
- **Training progress**: Monitor JSON logs in Logs/PyTrain/ directory
- **Model loading**: Verify paths and permissions for model files
- **Data loading**: Ensure image folders follow expected structure

## Cross-Platform Considerations

### Path Handling
- Use Path.Combine() instead of string concatenation
- Respect platform-specific separators (App.Separator property)
- Handle case sensitivity for non-Windows platforms

### Shell Execution
- CmdHelper automatically detects OS and uses appropriate shell
- Environment variables set for UTF-8 encoding in Python processes
- Virtual environment activation handles .bat vs bash script differences

## Logging and Monitoring

### Application Logs (.NET)
- **Serilog**: Structured logging to Logs/AppLogs/AutoTrainer.log
- **Daily rotation**: Automatic log file rotation by date

### Training Logs (Python)
- **JSON format**: Real-time training metrics in Logs/PyTrain/
- **Structured data**: Epoch-wise loss, accuracy, learning rate tracking
- **Error capture**: Full traceback logging for debugging

## Performance Considerations

### Memory Management
- Image loading and preprocessing optimized for batch processing
- GPU memory usage monitoring in training scripts
- Progressive image loading for large datasets

### Async Operations
- Python script execution is async to prevent UI blocking
- Progress reporting through delegate callbacks
- Cancellation token support for long-running operations