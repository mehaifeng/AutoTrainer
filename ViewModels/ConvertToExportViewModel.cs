using AutoTrainer.Helpers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Styles.Controls;
using Material.Styles.Models;
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
        public async Task ConvertToONNX(SnackbarHost o)
        {
            var onnxFolder = Path.Combine(App.ModelOutputFolderPath ?? string.Empty, "onnx");
            Directory.CreateDirectory(onnxFolder);
            var modelOutputPath = App.TrainModel.ModelOutputPath ?? string.Empty;
            var pretrainedModel = App.TrainModel.PretrainedModel ?? string.Empty;
            var modelPath = Path.Combine(modelOutputPath, pretrainedModel + ".pth");
            var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "ModelConverter.py");
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
            SnackbarHost.Post(
            new SnackbarModel(
                "模型已转换为onnx",
                TimeSpan.FromSeconds(8),
                new SnackbarButtonModel
                {
                    Text = "打开目录",
                    Action = () =>
                    {
                        var psi = new ProcessStartInfo();
                        if (OperatingSystem.IsWindows())
                        {
                            psi.FileName = "explorer";
                            psi.Arguments = onnxFolder;
                        }
                        else if (OperatingSystem.IsLinux())
                        {
                            psi.FileName = "xdg-open";
                            psi.Arguments = onnxFolder;
                        }
                        else if (OperatingSystem.IsMacOS())
                        {
                            psi.FileName = "open";
                            psi.Arguments = onnxFolder;
                        }
                        else
                        {
                            throw new PlatformNotSupportedException("不支持的操作系统");
                        }

                        try
                        {
                            Process.Start(psi);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"打开目录失败: {ex.Message}");
                        }
                    }
                }),
            o.HostName,
            DispatcherPriority.Normal);
        }
        /// <summary>
        /// 转换为TensorFlow
        /// </summary>
        [RelayCommand]
        public async Task ConvertToTensorFlow(SnackbarHost o)
        {
            var tensorflowFolder = Path.Combine(App.ModelOutputFolderPath ?? string.Empty, "tensorflow");
            Directory.CreateDirectory(tensorflowFolder);
            var modelOutputPath = App.TrainModel.ModelOutputPath ?? string.Empty;
            var pretrainedModel = App.TrainModel.PretrainedModel ?? string.Empty;
            var modelPath = Path.Combine(modelOutputPath, pretrainedModel + ".pth");
            var sb = new StringBuilder();
            var venvFolder = App.PythonVenvPath;
            var pythonScript = $"{Environment.CurrentDirectory}\\PyScripts\\ModelConverter.py";
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
            SnackbarHost.Post(
            new SnackbarModel(
                "模型已转换为tensorflow",
                TimeSpan.FromSeconds(8),
                new SnackbarButtonModel
                {
                    Text = "打开目录",
                    Action = () =>
                    {
                        var psi = new ProcessStartInfo();
                        psi.FileName = @"c:\windows\explorer.exe";
                        psi.Arguments = tensorflowFolder;
                        Process.Start(psi);
                    }
                }),
            o.HostName,
            DispatcherPriority.Normal);
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
