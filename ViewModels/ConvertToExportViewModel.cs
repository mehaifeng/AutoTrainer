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
        public ConvertToExportViewModel()
        {

        }
        [ObservableProperty]
        private string modelName;
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
            var onnxFolder = Path.Combine(App.ModelOutputFolderPath, "onnx");
            Directory.CreateDirectory(onnxFolder);
            var modelPath = Path.Combine(App.TrainModel.ModelOutputPath, App.TrainModel.PretrainedModel + ".pth");
            var pythonScript = Path.Combine(Environment.CurrentDirectory,"PyScripts","ModelConverter.py");
            StringBuilder sb = new StringBuilder();
            sb.Append($" --model {App.TrainModel.PretrainedModel}");
            sb.Append($" --format onnx");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(onnxFolder, App.TrainModel.PretrainedModel)}");
            var arguments = sb.ToString();
            IsConverting = true;
            IsEnableConvert = false;
            var result = await CmdHelper.ExecutePythonScriptAsync(pythonScript,App.PythonVenvPath,arguments,isShowTerminal: false, HandleOutput,System.Threading.CancellationToken.None);
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
                        psi.FileName = @"c:\windows\explorer.exe";
                        psi.Arguments = onnxFolder;
                        Process.Start(psi);
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
            var tensorflowFolder = Path.Combine(App.ModelOutputFolderPath, "tensorflow");
            Directory.CreateDirectory(tensorflowFolder);
            var modelPath = Path.Combine(App.TrainModel.ModelOutputPath, App.TrainModel.PretrainedModel + ".pth");
            var sb = new StringBuilder();
            var venvFolder = App.PythonVenvPath;
            var pythonScript = $"{Environment.CurrentDirectory}\\PyScripts\\ModelConverter.py";
            sb.Append($" --model {App.TrainModel.PretrainedModel}");
            sb.Append($" --format tensorflow");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(tensorflowFolder, App.TrainModel.PretrainedModel)}");
            var argument = sb.ToString();
            IsConverting = true;
            IsEnableConvert = false;
            await CmdHelper.ExecutePythonScriptAsync(pythonScript,venvFolder,argument,false, HandleOutput);
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
