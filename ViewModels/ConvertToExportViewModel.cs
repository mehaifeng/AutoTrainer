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
            var sb = new StringBuilder();
            sb.Append($"{App.PythonVenvPath}\\Scripts\\activate.bat");
            sb.Append(" && ");
            sb.Append($"python {Environment.CurrentDirectory}\\PyScripts\\ModelConverter.py");
            sb.Append($" --model {App.TrainModel.PretrainedModel}");
            sb.Append($" --format onnx");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(onnxFolder, App.TrainModel.PretrainedModel)}");
            sb.AppendLine();
            IsConverting = true;
            IsEnableConvert = false;
            await CmdHelper.ExecuteCmdWindow(sb.ToString(), true);
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
            sb.Append($"{App.PythonVenvPath}\\Scripts\\activate.bat");
            sb.Append(" && ");
            sb.Append($"python {Environment.CurrentDirectory}\\PyScripts\\ModelConverter.py");
            sb.Append($" --model {App.TrainModel.PretrainedModel}");
            sb.Append($" --format tensorflow");
            sb.Append($" --weights {modelPath}");
            sb.Append($" --output {Path.Combine(tensorflowFolder, App.TrainModel.PretrainedModel)}");
            sb.AppendLine();
            IsConverting = true;
            IsEnableConvert = false;
            await CmdHelper.ExecuteCmdWindow(sb.ToString(), true);
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
    }
}
