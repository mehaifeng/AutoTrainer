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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace AutoTrainer.ViewModels
{
    public partial class ModelConfigViewModel : ViewModelBase
    {
        #region 构造函数
        public ModelConfigViewModel()
        {
            ModelList = [];
            Requirements = string.Join("\r\n", requireApps);
            Task.Run(GetPython);
        }
        #endregion

        #region 全局属性
        private string modelHelperScript = Path.Combine($"{Environment.CurrentDirectory}","PyScripts","ModelHelper.py");
        private string[] requireApps = [
            "torch",
            "torchvision",
            "opencv-python",
            "pillow",
            "Matplotlib",
            "scikit-learn",
            "albumentations",
            "tqdm",
            "onnx",
            "onnx2tf",
            "tensorflow",
            "tf_keras",
            "psutil",
            "sympy",
            "six",
            "onnx-graphsurgeon",
            "sng4onnx"];
        private readonly HashSet<string> excludedPaths =
        [
            @"C:\Windows",
            @"C:\Documents and Settings",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData",
            @"System Volume Information",
            @"$RECYCLE.BIN"
        ];
        private List<string> missingApps = [];
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
        private string environmentState = "Environment State";
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

        public class PackageInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }

        public class CheckResult
        {
            public bool IsMatch { get; set; }
            public List<string> MissingPackages { get; set; }
            public string Message { get; set; }
        }

        #region 函数
        /// <summary>
        /// 找到Python
        /// </summary>
        private async Task GetPython()
        {
            var command = OperatingSystem.IsWindows() ? "where python" : "which python";
            var result = await CmdHelper.ExecuteLine(command);
            if (result.ExitCode == 0)
            {
                if (!string.IsNullOrEmpty(result.Output))
                {
                    var splitChart = OperatingSystem.IsWindows() ? "\r\n" : OperatingSystem.IsLinux()? "\n" : "\r";
                    var paths = result.Output.Split(splitChart);
                    PythonPath = paths[0];
                }
            }
            sb.Append(result.Output);
            sb.Append(result.Error);
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
                StringBuilder sb = new StringBuilder();
                var activateFile = OperatingSystem.IsWindows()? $"{PythonVenvPath}\\Scripts\\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                sb.Append(activateFile);
                sb.Append(" && ");
                sb.Append($"python {modelHelperScript} list");
                var command = sb.ToString();
                var result = await CmdHelper.ExecuteLine(command);
                if (result.ExitCode == 0)
                {
                    if (result.Output.Contains("###Models###"))
                    {
                        var modelNames = result.Output.Split("###Models###")[1].TrimStart().TrimEnd().Split(App.LineBreak);
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
            return excludedPaths.Any(excluded =>
                path.Contains(excluded, StringComparison.OrdinalIgnoreCase));
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
        private PackageInfo ParsePackageLine(string line)
        {
            // 处理空行或无效输入
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // 分割包名和版本号
            var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new PackageInfo
            {
                Name = parts[0].Trim().ToLowerInvariant(),
                Version = parts.Length > 1 ? parts[1].Trim() : string.Empty
            };
        }
        public CheckResult CheckPackages(string installedPackagesStr, IEnumerable<string> requiredPackages)
        {
            var result = new CheckResult
            {
                IsMatch = true,
                MissingPackages = new List<string>(),
                Message = string.Empty
            };

            try
            {
                // 解析已安装的包
                var installedPackages = installedPackagesStr
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => ParsePackageLine(line))
                    .Where(pkg => pkg != null)
                    .ToDictionary(pkg => pkg.Name, pkg => pkg.Version);

                // 检查所需的包
                foreach (var requiredPackage in requiredPackages.Select(p => p.Trim().ToLowerInvariant()))
                {
                    if (string.IsNullOrWhiteSpace(requiredPackage))
                        continue;

                    if (!installedPackages.ContainsKey(requiredPackage))
                    {
                        result.IsMatch = false;
                        result.MissingPackages.Add(requiredPackage);
                    }
                }
                // 构建结果信息
                if (!result.IsMatch)
                {
                    var missingPackagesMsg = string.Join("\n", result.MissingPackages.Select(p => $"Require {p}, but not install"));
                    sb.AppendLine(missingPackagesMsg);
                    result.Message = sb.ToString();
                }
                else
                {
                    result.Message = "Python软件包全部匹配";
                }
                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    IsMatch = false,
                    MissingPackages = new List<string>(),
                    Message = $"检查过程中发生错误: {ex.Message}"
                };
            }
        }
        private void HandleOutput(string data)
        {
            // 实时处理每行输出
            Debug.WriteLine(data);
            // 或者更新UI
            sb.AppendLine(data);
            Outputs = sb.ToString();
        }
        #endregion

        #region 命令
        /// <summary>
        /// 扫描Venv环境
        /// </summary>
        /// <returns></returns>
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
            await CmdHelper.ExecuteLine($"python -m venv {venvPath}");
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
                missingApps = [];
                //验证所填venv环境是否可用
                bool isVenvValid = CmdHelper.IsVenvValid(PythonVenvPath);
                if (isVenvValid)
                {
                    IsExcutingPyScript = true;
                    App.PythonVenvPath = PythonVenvPath;
                    var sb = new StringBuilder();
                    var activateFile = OperatingSystem.IsWindows()? $"{PythonVenvPath}\\Scripts\\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                    sb.Append(activateFile);
                    sb.Append(" && ");
                    sb.Append("pip list");
                    var command = sb.ToString();
                    var result = await CmdHelper.ExecuteLine(command);
                    if (result.ExitCode == 0)
                    {
                        string envName = $"({PythonVenvPath.Split("\\").Last()})";
                        PipApps = result.Output.TrimStart().TrimEnd();
                    }
                    sb.Append(result.Output);
                    Outputs = sb.ToString();
                    try
                    {
                        var checkResult = CheckPackages(PipApps, requireApps);
                        if (!checkResult.IsMatch)
                        {
                            environmentState = "Python软件包不匹配";
                            sb.Append(checkResult.Message);
                            Outputs = sb.ToString();
                            StateForeground = Brushes.Red;
                            IsVisibleInstallMissing = true;
                            missingApps.AddRange(checkResult.MissingPackages);
                        }
                        else
                        {
                            await GetModels();
                            environmentState = "Python软件包已安装";
                            StateForeground = Brushes.Green;
                            IsVisibleInstallMissing = false;
                        }
                    }
                    catch(Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard("错误", $"检查环境时发生错误：{ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
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
            if (missingApps.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                var activateFile = OperatingSystem.IsWindows()? $@"{PythonVenvPath}\Scripts\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                sb.Append(activateFile);
                foreach (var missingApp in missingApps)
                {
                    sb.Append($"&& pip install {missingApp}");
                }
                var command = sb.ToString();
                try
                {
                    // 设置进度条状态
                    IsVisibleProgressBar = true;
                    IsRunningProgressBar = true;
                    // 安装缺失的软件包
                    await CmdHelper.ExecuteLine(command, isShowTerminal:false,onOutputReceived: HandleOutput);
                    // 重新执行Python脚本
                    await ExecutePy();
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
                StringBuilder sb = new StringBuilder();
                var activateFile = OperatingSystem.IsWindows()? $"{PythonVenvPath}\\Scripts\\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                sb.Append(activateFile);
                sb.Append(" && ");
                sb.Append($"python {modelHelperScript} info {SelectModel}");
                var command = sb.ToString();
                var result = await CmdHelper.ExecuteLine(command);
                IsLoadingModelList = false;
                if (result.ExitCode == 0)
                {
                    if (result.Output.Contains("###ModelInfo###"))
                    {
                        SelectModelIntroduce = result.Output.Split("###ModelInfo###")[1].TrimStart().TrimEnd();
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
