using AutoTrainer.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ModelConfigViewModel : ViewModelBase
    {
        #region 构造函数
        public ModelConfigViewModel()
        {
            ModelList = [];
            Requirements = string.Join("\r\n", RequireApps);
            Task.Run(GetPython);
        }
        #endregion

        #region 全局属性
        private string[] RequireApps = [
            "torch",
            "torchvision",
            "opencv-python",
            "pillow",
            "Matplotlib",
            "scikit-learn",
            "albumentations",
            "tqdm",
            "onnx2tf",
            "tensorflow",
            "tf_keras",
        "psutil",
        "onnx-graphsurgeon"];
        private HashSet<string> ExcludedPaths =
        [
            @"C:\Windows",
                        @"C:\Documents and Settings",
                        @"C:\Program Files",
                        @"C:\Program Files (x86)",
                        @"C:\ProgramData",
                        @"C:\System Volume Information",
                        @"C:\$Recycle.Bin"
        ];
        private List<string> MissingApps = [];
        StringBuilder sb = new StringBuilder();
        #endregion

        #region 绑定属性

        [ObservableProperty]
        private string pythonPath;
        [ObservableProperty]
        private string pythonVenvPath;
        [ObservableProperty]
        private string pipApps;
        [ObservableProperty]
        private string requirements;
        [ObservableProperty]
        private bool isEnablePythonConfigView = true;
        [ObservableProperty]
        private string enviromentState = "Environment State";
        [ObservableProperty]
        private IBrush stateForeground = Brushes.Green;
        [ObservableProperty]
        private ObservableCollection<string> modelList;
        [ObservableProperty]
        private string selectModel;
        [ObservableProperty]
        private string selectModelIntroduce;
        [ObservableProperty]
        private bool isLoadingModelList = false;
        [ObservableProperty]
        private bool isVisibleIntroduce;
        [ObservableProperty]
        private string outputs;
        [ObservableProperty]
        private bool isCheckedGlobal;
        [ObservableProperty]
        private bool isCheckedVenv = true;
        [ObservableProperty]
        private bool isVisibleInstallMissing = false;
        [ObservableProperty]
        private bool isVisibleProgressBar = false;
        [ObservableProperty]
        private bool isRunningProgressBar = false;
        [ObservableProperty]
        private bool isExcutingPyScript = false;
        [ObservableProperty]
        private bool isScanningVenv = false;
        [ObservableProperty]
        private string scanningFolder = string.Empty;
        #endregion

        #region 函数
        /// <summary>
        /// 找到Python
        /// </summary>
        private async Task GetPython()
        {
            string command = "/c where python";
            string fileName = "cmd.exe";
            (int, string, string) result = await CmdHelper.ExecuteLine(fileName, command);
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
        /// <summary>
        /// 获取模型列表
        /// </summary>
        /// <returns></returns>
        private async Task GetModels()
        {
            if (!string.IsNullOrEmpty(PythonVenvPath))
            {
                ModelList = new ObservableCollection<string>();
                List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", $"python {AppDomain.CurrentDomain.BaseDirectory}PyScripts\\ModelHelper.py list", $"{PythonVenvPath}\\Scripts\\deactivate.bat"];
                var result = await CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                if (result.Item1 == 0)
                {
                    if (result.Item2.Contains("###Models###"))
                    {
                        var modelNames = result.Item2.Split("###Models###")[1].TrimStart().TrimEnd().Split("\r\n");
                        for (int i = 0; i < modelNames.Length; i++)
                        {
                            var name = modelNames[i].ToLower();
                            if (name.StartsWith("resnet") || name.StartsWith("efficientnet") || name.StartsWith("mobilenet") || name.StartsWith("densenet") || name.StartsWith("vgg"))
                            {
                                ModelList.Add(name);
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 是否跳过目录
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool ShouldSkipDirectory(string path)
        {
            return ExcludedPaths.Any(excluded =>
                path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }
        /// <summary>
        /// 扫描Venv目录
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task ScanDirectory(string path)
        {
            await Task.Run(async () =>
            {
                try
                {
                    if (ShouldSkipDirectory(path)) return;
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        if (ShouldSkipDirectory(dir)) continue;
                        ScanningFolder = dir;
                        // 检查是否为venv目录
                        if (IsVenvDirectory(dir))
                        {
                            sb.AppendLine($"找到虚拟环境：{dir}");
                            Outputs = sb.ToString();
                        }
                        ScanDirectory(dir).Wait();
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception ex)
                {
                    await MessageBoxManager.GetMessageBoxStandard("扫描识别", $"警告：扫描目录 {path} 时出错：{ex.Message}\n", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                }
            });
        }
        /// <summary>
        /// 是否为Venv目录
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsVenvDirectory(string path)
        {
            try
            {
                // 检查典型的venv目录特征
                var pyvenvCfg = Path.Combine(path, "pyvenv.cfg");
                var scriptsDir = Path.Combine(path, "Scripts");
                var binDir = Path.Combine(path, "bin");
                var libDir = Path.Combine(path, "Lib", "site-packages");

                return (File.Exists(pyvenvCfg) &&
                       (Directory.Exists(scriptsDir) || Directory.Exists(binDir)) &&
                       Directory.Exists(libDir));
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region 命令
        [RelayCommand]
        public async Task ScanningVenv()
        {
            IsScanningVenv = true;
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed)
                    .Select(d => d.RootDirectory.FullName);

                foreach (var drive in drives)
                {
                    try
                    {
                        await ScanDirectory(drive);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard("扫描识别",$"扫描驱动器 {drive} 时出错：{ex.Message}\n",MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard($"发生错误：{ex.Message}", "错误").ShowWindowAsync();
            }
            IsScanningVenv = false;
        }
        /// <summary>
        /// 创建Venv环境
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task CreateVenv()
        {
            string venvFolder = Path.Combine(Environment.CurrentDirectory, "Venvs");
            Directory.CreateDirectory(venvFolder);
            string venvPath = Path.Combine(venvFolder, DateTime.Now.ToString("yyMMddHHmmss_Venv"));
            IsEnablePythonConfigView = false;
            await CmdHelper.ExecuteCmdWindow($"python -m venv {venvPath}", false);
            IsEnablePythonConfigView = true;
            PythonVenvPath = venvPath;
        }
        /// <summary>
        /// 进入Venv环境，执行Pip List
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task ExecutePy()
        {
            if (IsCheckedVenv && !string.IsNullOrEmpty(PythonVenvPath))
            {
                //验证所填venv环境是否可用
                bool isVenvValid = CmdHelper.IsVenvValid(PythonVenvPath);
                if (isVenvValid)
                {
                    IsExcutingPyScript = true;
                    App.PythonVenvPath = PythonVenvPath;
                    List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", "pip list"];
                    (int, string) result = await CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                    if (result.Item1 == 0)
                    {
                        string envName = $"({PythonVenvPath.Split("\\").Last()})";
                        PipApps = result.Item2.Split("pip list")[1].Split(envName)[0].TrimStart().TrimEnd();
                    }
                    sb.Append(result.Item2);
                    Outputs = sb.ToString();
                    //判断pip软件包是否包含了要求的软件包
                    var apps = PipApps.Split("\r\n");
                    bool isMatch = true;
                    foreach (var require in RequireApps)
                    {
                        for (int i = 0; i < apps.Length; i++)
                        {
                            if (apps[i].ToLower().Contains(require.ToLower()))
                            {
                                break;
                            }
                            if (!apps[i].ToLower().Contains(require.ToLower()) && i == apps.Length - 1)
                            {
                                sb.AppendLine($"require {require}, but not install");
                                MissingApps.Add(require);
                                isMatch = false;
                            }
                        }
                    }
                    if (!isMatch)
                    {
                        EnviromentState = "Environment State: No Match";
                        StateForeground = Brushes.Red;
                        IsVisibleInstallMissing = true;
                    }
                    else
                    {
                        await GetModels();
                        EnviromentState = "Environment State: All Match";
                        StateForeground = Brushes.Green;
                        IsVisibleInstallMissing = false;
                    }
                    IsExcutingPyScript = false;
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
        /// <summary>
        /// 安装需要的软件包
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task InstallMissingApp()
        {
            if (MissingApps.Count > 0)
            {
                StringBuilder commands = new StringBuilder();
                commands.Append($"{PythonVenvPath}\\Scripts\\activate.bat");
                foreach (var missingApp in MissingApps)
                {
                    commands.Append($"&& pip install {missingApp}");
                }

                try
                {
                    // 设置进度条状态
                    IsVisibleProgressBar = true;
                    IsRunningProgressBar = true;


                    // 创建并等待所有任务完成
                    Task[] commonTasks = [CmdHelper.ExecuteCmdWindow(commands.ToString(), true), ExecutePy()];

                    // 等待所有任务完成
                    await Task.WhenAll(commonTasks);
                }
                finally
                {
                    // 在任务完成或发生异常时都能重置进度条状态
                    IsVisibleProgressBar = false;
                    IsRunningProgressBar = false;
                }
            }
        }
        /// <summary>
        /// 选择模型命令
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task SelectModelToShow()
        {
            if (!string.IsNullOrEmpty(SelectModel))
            {
                IsLoadingModelList = true;
                App.TrainModel.PretrainedModel = SelectModel;
                List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", $"python {AppDomain.CurrentDomain.BaseDirectory}PyScripts\\ModelHelper.py info {SelectModel}", $"{PythonVenvPath}\\Scripts\\deactivate.bat"];
                var result = await CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                IsLoadingModelList = false;
                if (result.Item1 == 0)
                {
                    if (result.Item2.Contains("###ModelInfo###"))
                    {
                        SelectModelIntroduce = result.Item2.Split("###ModelInfo###")[1].TrimStart().TrimEnd();
                        IsVisibleIntroduce = true;
                    }
                }
            }
        }
        /// <summary>
        /// 转到下一页
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [RelayCommand]
        public static async Task GoToNextTab(UserControl o)
        {
            if (o != null && o.Parent != null)
            {
                if (o.Parent.Parent is TabControl control)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        control.SelectedIndex = 1;
                    });
                }
            }
        }
        #endregion
    }
}
