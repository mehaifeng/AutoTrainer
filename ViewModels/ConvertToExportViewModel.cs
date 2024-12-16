using AutoTrainer.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
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
            ModelName = App.TrainModel.PretrainedModel;
        }
        [ObservableProperty]
        private string modelName;
        [ObservableProperty]
        private bool isConverting = false;
        /// <summary>
        /// 转换为ONNX
        /// </summary>
        [RelayCommand]
        public async void ConvertToONNX()
        {
            var onnxFolder = Path.Combine(App.ModelOutputFolderPath, "ONNX");
            System.IO.Directory.CreateDirectory(onnxFolder);
            var modelPath = App.TrainModel.ModelOutputPath;
            var sb = new StringBuilder();
            sb.AppendLine($"{App.PythonVenvPath}\\Scripts\\activate.bat");
            sb.AppendLine(" && ");
            sb.AppendLine($"python {Environment.CurrentDirectory}\\PyScripts\\ModelConverter.py");
            sb.AppendLine($" --model {App.TrainModel.PretrainedModel}");
            sb.AppendLine($" --format onnx");
            sb.AppendLine($" --weights {modelPath}");
            sb.AppendLine($" --output {Path.Combine(onnxFolder, "ONNX")}");
            IsConverting = true;
            await CmdHelper.ExecuteCmdWindow(sb.ToString(), true);
            IsConverting = false;
        }
        /// <summary>
        /// 转换为TensorFlow
        /// </summary>
        [RelayCommand]
        public void ConvertToTensorFlow()
        {

        }
    }
}
