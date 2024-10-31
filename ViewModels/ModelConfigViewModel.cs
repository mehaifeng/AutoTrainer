using AutoTrainer.CmdHelper;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ModelConfigViewModel : ViewModelBase
    {
        public ModelConfigViewModel()
        {
            Requirements = string.Join("\r\n", RequireApps);
            GetPython();
        }
        private string[] RequireApps = ["torch", "torchvision", "opencv-python", "pillow", "Matplotlib", "scikit-learn", "albumentations", "tqdm"];
        [ObservableProperty]
        private string pythonPath;
        [ObservableProperty]
        private string pythonVenvPath;
        [ObservableProperty]
        private string pipApps;
        [ObservableProperty]
        private string requirements;
        [ObservableProperty]
        private string enviromentState = "Environment State";
        [ObservableProperty]
        private IBrush stateForeground = Brushes.Green;
        [ObservableProperty]
        private string outputs;
        [ObservableProperty]
        private bool isCheckedGlobal;
        [ObservableProperty]
        private bool isCheckedVenv = true;

        StringBuilder sb = new StringBuilder();
        /// <summary>
        /// 找到Python
        /// </summary>
        private async void GetPython()
        {
            string command = "/c where python";
            string fileName = "cmd.exe";
            (int, string, string) result = await CmdHelper.CmdHelper.ExecuteLine(fileName, command);
            if (result.Item1 == 0)
            {
                if (!string.IsNullOrEmpty(result.Item2))
                {
                    var paths = result.Item2.Split("\r\n");
                    PythonPath = paths[0];
                }
            }
            sb.Append(result.Item2);
            sb.Append(result.Item3);
            Outputs = sb.ToString();
        }
        [RelayCommand]
        public async Task ExecutePy()
        {
            if (IsCheckedVenv && !string.IsNullOrEmpty(PythonVenvPath))
            {
                //验证所填venv环境是否可用
                bool isVenvValid = CmdHelper.CmdHelper.IsVenvValid(PythonVenvPath);
                if (isVenvValid)
                {
                    List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", "pip list", $"Python {AppDomain.CurrentDomain.BaseDirectory}PyScripts\\testdevice.py"];
                    (int, string) result = await CmdHelper.CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                    if (result.Item1 == 0)
                    {
                        string envName = $"({PythonVenvPath.Split("\\").Last()})";
                        PipApps = result.Item2.Split("----------------- ---------")[1].Split(envName)[0].TrimStart().TrimEnd();
                    }
                    sb.Append(result.Item2);
                    Outputs = sb.ToString();
                    //判断pip软件包是否包含了要求的软件包
                    var apps = PipApps.Split("\r\n");
                    bool isMatch = true;
                    foreach (var app in apps)
                    {

                    }
                    if (!isMatch)
                    {
                        EnviromentState = "Environment State: No Match";
                        StateForeground = Brushes.Red;
                    }
                    else
                    {
                        EnviromentState = "Environment State: All Match";
                        StateForeground = Brushes.Green;
                    }
                    Outputs = sb.ToString();
                }
                else
                {
                    sb.AppendLine("Venv path is unavailable");
                    Outputs = sb.ToString();
                }
            }
            else
            {
                sb.Append("Global enviroment is not ready");
                Outputs = sb.ToString();
            }
        }
    }
}
