# GEMINI Project Analysis

## Project Overview

This is a .NET desktop application named "AutoTrainer" built with Avalonia UI. It serves as a graphical user interface (GUI) for training image classification and regression models using PyTorch. The application manages the entire machine learning workflow, from environment setup and data annotation to model training, validation, and export.

**Key Technologies:**

*   .NET 8.0
*   Avalonia (for the user interface)
*   Python
*   PyTorch (for machine learning)
*   Serilog (for logging)

**Architecture:**

The project follows a hybrid architecture, combining a .NET frontend with a Python backend for machine learning tasks.

*   **.NET Frontend:** The Avalonia application provides the user interface for configuring training parameters, managing datasets, and visualizing results. It generates a `ModelParam.json` configuration file that is consumed by the Python backend.
*   **Python Backend:** A set of Python scripts (`PyScripts/`) handles the core machine learning logic. `ModelTrainer.py` is the main script responsible for training the PyTorch models based on the provided JSON configuration.
*   **Communication:** The .NET application executes the Python scripts using `CmdHelper.cs`, which manages the execution of shell commands and the activation of Python virtual environments.

## Building and Running

The project can be built and run using the .NET CLI or by opening the `AutoTrainer.sln` file in an IDE like Visual Studio.

**Build Command:**

```bash
dotnet build
```

**Running the Application:**

```bash
dotnet run --project AutoTrainer.csproj
```

The main application executable will be located in the `bin/Debug/net8.0` directory after building.

## Development Conventions

*   The .NET application uses the MVVM (Model-View-ViewModel) pattern.
*   The UI is built with Avalonia and uses a tab-based navigation to guide the user through the training workflow.
*   The Python scripts are designed to be executed from the command line and receive their configuration via a JSON file.
*   Logging is implemented using Serilog in the .NET application and a custom `TrainingLogger` in the Python scripts.
*   The application manages Python virtual environments to ensure that the correct dependencies are used for training.
