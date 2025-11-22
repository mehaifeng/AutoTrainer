using AutoTrainer.Helpers;
using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ConvertToExportViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string? modelName;
        [ObservableProperty]
        private bool isConverting = false;
        [ObservableProperty]
        private bool isEnableConvert = true;
        [ObservableProperty]
        private string convertingOutput = "";
        [ObservableProperty]
        private int caretEndIndex = 0;

        [RelayCommand]
        public void LoadConverterPage()
        {
            ModelName = App.TrainModel.PretrainedModel;
        }
        /// <summary>
        /// 转换为ONNX
        /// </summary>
        [RelayCommand]
        public async Task ConvertToONNX()
        {
            var onnxFolder = Path.Combine(App.ModelOutputFolderPath ?? string.Empty, "onnx");
            Directory.CreateDirectory(onnxFolder);
            var modelOutputPath = App.TrainModel.ModelOutputPath ?? string.Empty;
            var pretrainedModel = App.TrainModel.PretrainedModel ?? string.Empty;
            var modelPath = Path.Combine(modelOutputPath, pretrainedModel + ".pth");
            var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "Conversion", "ModelConverter.py");
            StringBuilder sb = new StringBuilder();
            sb.Append($" --model {pretrainedModel}");
            sb.Append($" --format onnx");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(onnxFolder, pretrainedModel)}");
            var arguments = sb.ToString();
            IsConverting = true;
            IsEnableConvert = false;
            var result = await CliWrapHelper.ExecutePythonScriptAsync(pythonScript, App.PythonVenvPath, arguments, isShowTerminal: false, HandleOutput, System.Threading.CancellationToken.None);
            IsConverting = false;
            IsEnableConvert = true;
            // 转换完成，可以在输出窗口中查看结果
        }
        /// <summary>
        /// 转换为TensorFlow
        /// </summary>
        [RelayCommand]
        public async Task ConvertToTensorFlow()
        {
            var tensorflowFolder = Path.Combine(App.ModelOutputFolderPath ?? string.Empty, "tensorflow");
            Directory.CreateDirectory(tensorflowFolder);
            var modelOutputPath = App.TrainModel.ModelOutputPath ?? string.Empty;
            var pretrainedModel = App.TrainModel.PretrainedModel ?? string.Empty;
            var modelPath = Path.Combine(modelOutputPath, pretrainedModel + ".pth");
            var sb = new StringBuilder();
            var venvFolder = App.PythonVenvPath;
            var pythonScript = $"{Environment.CurrentDirectory}\\PyScripts\\Conversion\\ModelConverter.py";
            sb.Append($" --model {pretrainedModel}");
            sb.Append($" --format tensorflow");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(tensorflowFolder, pretrainedModel)}");
            var argument = sb.ToString();
            IsConverting = true;
            IsEnableConvert = false;
            await CliWrapHelper.ExecutePythonScriptAsync(pythonScript, venvFolder, argument, false, HandleOutput);
            IsConverting = false;
            IsEnableConvert = true;
            // 转换完成，可以在输出窗口中查看结果
        }

        private void HandleOutput(string data)
        {
            // 实时处理每行输出
            Debug.WriteLine(data);
            // 或者更新UI
            ConvertingOutput += "\n"+data;
        }
    }
}
