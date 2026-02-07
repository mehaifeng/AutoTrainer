using AutoTrainer.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using Serilog;

namespace AutoTrainer.ViewModels
{
    public partial class SetupViewModel : ViewModelBase
    {
        #region 构造函数
        public SetupViewModel()
        {
            ModelList = [];

            // 初始化任务类型
            TaskTypes = new ObservableCollection<TaskTypeInfo>
            {
                new TaskTypeInfo { Name = "图像分类", Value = "classification" },
                new TaskTypeInfo { Name = "目标检测", Value = "detection" }
            };

            SelectedTaskType = TaskTypes[0]; // 默认选择分类
            Task.Run(SetRequirementsDisplay);
            Task.Run(GetPython);
            
            // 初始加载模型列表
            Task.Run(async () => await GetModelsWithLoadingState());
        }
        #endregion

        #region 全局属性
        private string modelHelperScript = Path.Combine($"{Environment.CurrentDirectory}","PyScripts","Utils","ModelHelper.py");
        private string requirementsFilePath = Path.Combine(Environment.CurrentDirectory, "Configs", "Requirements.json");
        private string availableModelsFilePath = Path.Combine(Environment.CurrentDirectory, "Configs", "AvailableModels.json");
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
            "tensorflow",
            "tf_keras",
            "psutil",
            "sympy",
            "six",
            "onnx-graphsurgeon",
            "sng4onnx"];
        private List<string> missingApps = [];
        StringBuilder sb = new StringBuilder();
        #endregion

        #region 绑定属性

        [ObservableProperty]
        private string? pythonPath;
        [ObservableProperty]
        private string? pythonVenvPath;
        [ObservableProperty]
        private ObservableCollection<string> pythonVenvPaths = new();
        [ObservableProperty]
        private string? pipApps;
        [ObservableProperty]
        private string? requirements;
        [ObservableProperty]
        private bool isEnablePythonConfigView = true;
        [ObservableProperty]
        private string? environmentState = "Python环境状态";
        [ObservableProperty]
        private IBrush stateForeground = Brushes.Green;
        [ObservableProperty]
        private ObservableCollection<string> modelList;
        [ObservableProperty]
        private string? selectModel;
        [ObservableProperty]
        private string? selectedLocalWeightText;
        [ObservableProperty]
        private string? selectModelIntroduce;
        [ObservableProperty]
        private bool isLoadingModelList = false;
        [ObservableProperty]
        private bool isVisibleIntroduce;
        [ObservableProperty]
        private string? outputs;
        [ObservableProperty]
        private bool isVisibleInstallMissing = false;
        [ObservableProperty]
        private bool isVisibleProgressBar = false;
        [ObservableProperty]
        private bool isRunningProgressBar = false;
        [ObservableProperty]
        private bool isExecutingPyScript = false;
        [ObservableProperty]
        private bool isScanningVenv = false;
        [ObservableProperty]
        private string? scanningFolder = string.Empty;

        // 任务类型选择相关
        [ObservableProperty]
        private ObservableCollection<TaskTypeInfo> taskTypes = new();

        [ObservableProperty]
        private TaskTypeInfo? selectedTaskType;

        // 任务类型切换状态
        [ObservableProperty]
        private bool isTaskTypeChanging = false;

        // 环境诊断相关
        [ObservableProperty]
        private string? diagnosticReport;
        [ObservableProperty]
        private bool showDiagnosticReport = false;
        [ObservableProperty]
        private bool isRunningDiagnostics = false;

        // 环境诊断结果缓存
        private DiagnosticReport? _cachedDiagnosticReport;

        #endregion

        #region 函数
        /// <summary>
        /// 设置需求包显示（使用优化后的RequirementsManager）
        /// </summary>
        /// <returns></returns>
        private async Task SetRequirementsDisplay()
        {
            try
            {
                // 首先运行环境诊断
                _cachedDiagnosticReport = await EnvironmentDiagnostics.CheckEnvironment(PythonPath);

                // 根据诊断结果决定是否使用CUDA
                bool useCUDA = _cachedDiagnosticReport.HasCUDA;
                string? cudaVersion = _cachedDiagnosticReport.CUDAVersion;

                // 使用RequirementsManager加载需求包
                var packages = await RequirementsManager.LoadRequirementsAsync(useCUDA, cudaVersion);

                // 格式化显示
                Requirements = string.Join("\r\n", packages);

                // 记录诊断信息到输出
                if (_cachedDiagnosticReport.Warnings.Count > 0 || _cachedDiagnosticReport.Recommendations.Count > 0)
                {
                    sb.AppendLine("=== 环境检测 ===");
                    if (_cachedDiagnosticReport.HasCUDA)
                    {
                        sb.AppendLine($"✓ 检测到 CUDA {_cachedDiagnosticReport.CUDAVersion}");
                    }
                    else
                    {
                        sb.AppendLine("○ 未检测到CUDA，将使用CPU版本");
                    }

                    foreach (var warning in _cachedDiagnosticReport.Warnings)
                    {
                        sb.AppendLine($"⚠ {warning}");
                    }
                    Outputs = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "设置需求包显示时出错");
                // 回退到旧方法
                await SetRequirementsDisplayFallback();
            }
        }

        /// <summary>
        /// 设置需求包显示（回退方法，保持向后兼容）
        /// </summary>
        private async Task SetRequirementsDisplayFallback()
        {
            GetRequirementPackages();

            var otherPackages = requireApps
                .Where(r => !r.Trim().StartsWith("torch"))
                .ToList();

            var torchPackages = new List<string>();

            var cudaResult = await CliWrapHelper.ExecuteLine("nvidia-smi");

            if (cudaResult.ExitCode == 0 && !string.IsNullOrEmpty(cudaResult.Output))
            {
                var match = System.Text.RegularExpressions.Regex.Match(cudaResult.Output, @"CUDA Version:\s*(\d+\.\d+)");
                if (match.Success)
                {
                    var cudaVersionStr = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(cudaVersionStr) && double.TryParse(cudaVersionStr, out double cudaVersion))
                    {
                        // PyTorch 官方只提供特定 CUDA 版本的预构建包
                        // CUDA 12.1+ 可使用 cu126
                        // CUDA 12.8+ 可使用 cu128
                        // CUDA 13.0+ 可使用 cu130
                        string torchCudaVersion;
                        if (cudaVersion >= 13.0)
                        {
                            torchCudaVersion = "cu130";
                        }
                        else if (cudaVersion >= 12.8)
                        {
                            torchCudaVersion = "cu128";
                        }
                        else if (cudaVersion >= 12.1)
                        {
                            torchCudaVersion = "cu126";
                        }
                        else
                        {
                            // CUDA 版本过低，使用 CPU 版本
                            torchCudaVersion = null;
                        }

                        if (!string.IsNullOrEmpty(torchCudaVersion))
                        {
                            torchPackages.Add($"torch --index-url https://download.pytorch.org/whl/{torchCudaVersion}");
                            torchPackages.Add($"torchvision --index-url https://download.pytorch.org/whl/{torchCudaVersion}");
                        }
                    }
                }
            }

            if (torchPackages.Count == 0)
            {
                // Default for CPU or if CUDA version not matched
                torchPackages.Add("torch");
                torchPackages.Add("torchvision");
            }

            var allDisplayPackages = new List<string>();
            allDisplayPackages.AddRange(torchPackages);
            allDisplayPackages.AddRange(otherPackages);

            Requirements = string.Join("\r\n", allDisplayPackages);
        }
        /// <summary>
        /// 获取需求包列表
        /// </summary>
        private void GetRequirementPackages()
        {
            if (File.Exists(requirementsFilePath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(requirementsFilePath);
                    var requirementsData = JsonConvert.DeserializeObject<RequirementsData>(jsonContent);
                    if (requirementsData?.Packages != null)
                    {
                        requireApps = requirementsData.Packages;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading requirements file: {ex.Message}");
                }
            }
        }

        private class RequirementsData
        {
            public string[] Packages { get; set; } = [];
        }
        /// <summary>
        /// 找到Python
        /// </summary>
        private async Task GetPython()
        {
            var command = OperatingSystem.IsWindows() ? "where python" : "which python3";
            var result = await CliWrapHelper.ExecuteLine(command);
            if (result.ExitCode == 0)
            {
                if (!string.IsNullOrEmpty(result.Output))
                {
                    var splitChart = OperatingSystem.IsWindows() ? "\r\n" : "\n";
                    var validPythons = new List<string>();
                    //所有的Python路径
                    var pythonPaths = result.Output.Split(splitChart);
                    foreach (var path in pythonPaths)
                    {
                        //if (path.Contains("WindowsApps") || string.IsNullOrEmpty(path))
                        //{
                        //    continue; // 跳过WindowsApps中的Python路径
                        //}
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }
                        else
                        {
                            var testCommand = string.Concat(path, " --version");
                            var validResult = await CliWrapHelper.ExecuteLine(testCommand);
                            if (validResult.Error != null && validResult.Output != null)
                            {
                                if (validResult.Error.Contains("Python"))
                                {
                                    continue;
                                }
                                else if(validResult.Output.Contains("Python"))
                                {
                                    validPythons.Add(path);
                                }
                            }
                        }
                    }
                    PythonPath = validPythons[0];
                }
            }
            sb.Append(result.Output);
            sb.Append(result.Error);
            Outputs = sb.ToString();
        }
        /// <summary>
        /// 从配置文件获取模型列表
        /// </summary>
        /// <returns></returns>
        private async Task GetModels()
        {
            await Task.Run(() =>
            {
                try
                {
                    // 确保在UI线程中初始化ModelList
                    Dispatcher.UIThread.Post(() => ModelList = new ObservableCollection<string>());

                    if (!File.Exists(availableModelsFilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"GetModels: 配置文件不存在 {availableModelsFilePath}");
                        return;
                    }

                    var jsonContent = File.ReadAllText(availableModelsFilePath);
                    var modelConfig = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>(jsonContent);

                    if (modelConfig == null || SelectedTaskType == null)
                    {
                        System.Diagnostics.Debug.WriteLine("GetModels: 配置为空或任务类型未选择");
                        return;
                    }

                    var taskType = SelectedTaskType.Value;
                    if (modelConfig.ContainsKey(taskType))
                    {
                        var taskModels = modelConfig[taskType];
                        foreach (var category in taskModels)
                        {
                            foreach (var modelName in category.Value)
                            {
                                // 确保在UI线程中更新ObservableCollection
                                Dispatcher.UIThread.Post(() => ModelList.Add(modelName));
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"GetModels: 从配置文件加载了 {ModelList.Count} 个模型");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"GetModels: 配置文件中未找到任务类型 {taskType}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetModels 异常: {ex.Message}");
                }
            });
        }
        /// <summary>
        /// 是否跳过目录
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool ShouldSkipDirectory(string path)
        {
            // 针对 Linux/macOS 系统根目录的精确匹配
            var unixSystemDirs = new[] 
            { 
                "/proc", "/sys", "/dev", "/boot", "/root", 
                "/bin", "/sbin", "/lib", "/lib64", 
                "/tmp", "/var/tmp", "/run", "/snap" 
            };
            
            if (!OperatingSystem.IsWindows())
            {
                // Linux/macOS: 只匹配根级系统目录或其直接子目录
                foreach (var sysDir in unixSystemDirs)
                {
                    if (path == sysDir || path.StartsWith(sysDir + "/", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            
            // Windows: 使用 Contains 匹配（如 C:\Windows\System32）
            var windowsExcludedPaths = new[] 
            { 
                "Windows", "Documents and Settings", 
                "Program Files", "Program Files (x86)", 
                "ProgramData", "System Volume Information", 
                "$RECYCLE.BIN", "$WinREAgent" 
            };
            
            if (OperatingSystem.IsWindows())
            {
                return windowsExcludedPaths.Any(excluded =>
                    path.Contains(excluded, StringComparison.OrdinalIgnoreCase));
            }
            
            return false;
        }
        /// <summary>
        /// 扫描Venv目录（递归，同步方法避免死锁）
        /// </summary>
        /// <param name="path"></param>
        private void ScanDirectoryRecursive(string path)
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
                        PythonVenvPaths.Add(dir);
                        PythonVenvPath ??= dir;
                        Outputs = sb.ToString();
                        
                        // 找到 venv 后不再递归进入其子目录（venv内部不会再有venv）
                        continue;
                    }
                    
                    // 递归扫描子目录（同步调用，避免 .Wait() 死锁）
                    ScanDirectoryRecursive(dir);
                }
            }
            catch (UnauthorizedAccessException) 
            { 
                // 静默忽略无权限目录
            }
            catch (DirectoryNotFoundException) 
            { 
                // 静默忽略不存在的目录
            }
            catch (IOException) 
            { 
                // 静默忽略IO错误
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "扫描目录 {Path} 时出错", path);
            }
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
                // 检查 pyvenv.cfg 文件（所有平台都有）
                var pyvenvCfg = Path.Combine(path, "pyvenv.cfg");
                if (!File.Exists(pyvenvCfg))
                {
                    return false;
                }

                // 检查可执行文件目录（Windows: Scripts/, Linux/macOS: bin/）
                var executableDir = OperatingSystem.IsWindows()
                    ? Path.Combine(path, "Scripts")
                    : Path.Combine(path, "bin");

                if (!Directory.Exists(executableDir))
                {
                    return false;
                }

                // 检查 site-packages 目录
                // Windows: Lib/site-packages
                // Linux/macOS: lib/pythonX.Y/site-packages
                if (OperatingSystem.IsWindows())
                {
                    var libDir = Path.Combine(path, "Lib", "site-packages");
                    return Directory.Exists(libDir);
                }
                else
                {
                    // Linux/macOS: 查找 lib/python*/site-packages
                    var libDir = Path.Combine(path, "lib");
                    if (!Directory.Exists(libDir))
                    {
                        return false;
                    }

                    // 查找任何 pythonX.Y 目录
                    foreach (var dir in Directory.GetDirectories(libDir, "python*"))
                    {
                        var sitePackages = Path.Combine(dir, "site-packages");
                        if (Directory.Exists(sitePackages))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// 处理包名
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private PackageInfo ParsePackageLine(string line)
        {
            // 处理空行或无效输入
            if (string.IsNullOrWhiteSpace(line))
            {
                return new PackageInfo
                {
                    Name = null,
                    Version = null
                };
            }

            // 分割包名和版本号
            var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new PackageInfo
            {
                Name = parts[0].Trim().ToLowerInvariant(),
                Version = parts.Length > 1 ? parts[1].Trim() : string.Empty
            };
        }
        /// <summary>
        /// 检查需求包是否安装
        /// </summary>
        /// <param name="installedPackagesStr"></param>
        /// <param name="requiredPackages"></param>
        /// <returns></returns>
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
                // 解析已安装的包，使用规范化包名作为键
                var installedPackages = installedPackagesStr
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => ParsePackageLine(line))
                    .Where(pkg => !string.IsNullOrEmpty(pkg.Name))
                    .ToDictionary(
                        pkg => NormalizePackageName(pkg.Name!),
                        pkg => pkg.Version,
                        StringComparer.OrdinalIgnoreCase
                    );

                var requiredPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 检查所需的包
                foreach (var requiredPackageLine in requiredPackages)
                {
                    if (string.IsNullOrWhiteSpace(requiredPackageLine))
                        continue;

                    var packageName = requiredPackageLine.Split(new[] { '=', '>', '<', ' ' }, 2)[0].Trim();
                    var normalizedName = NormalizePackageName(packageName);

                    if (requiredPackageNames.Contains(normalizedName))
                        continue;

                    if (!installedPackages.ContainsKey(normalizedName))
                    {
                        result.IsMatch = false;
                        if (normalizedName == "torch" || normalizedName == "torchvision")
                        {
                            if (!result.MissingPackages.Contains("torch"))
                            {
                                result.MissingPackages.Add("torch");
                            }
                        }
                        else
                        {
                            result.MissingPackages.Add(requiredPackageLine);
                        }
                    }
                    requiredPackageNames.Add(normalizedName);
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

        /// <summary>
        /// 规范化包名（遵循 PEP 503 规范）
        /// 将连字符、下划线、点替换为下划线，并转换为小写
        /// </summary>
        /// <param name="packageName">原始包名</param>
        /// <returns>规范化后的包名</returns>
        private static string NormalizePackageName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return string.Empty;

            // PEP 503: 将包名转换为小写，并将 [-_.] 替换为下划线
            var normalized = packageName
                .ToLowerInvariant()
                .Replace('-', '_')
                .Replace('.', '_');

            return normalized;
        }
        /// <summary>
        /// 内容输出
        /// </summary>
        /// <param name="data"></param>
        private void HandleOutput(string data)
        {
            // 实时处理每行输出
            Debug.WriteLine(data);
            // 或者更新UI
            sb.AppendLine(data);
            Outputs = sb.ToString();
        }
        /// <summary>
        /// 任务类型变更时的处理
        /// </summary>
        partial void OnSelectedTaskTypeChanged(TaskTypeInfo? value)
        {
            if (value != null)
            {
                if (App.TrainModel != null)
                {
                    // 更新全局配置
                    App.TrainModel.TaskType = value.Value;
                }

                // 清空之前的选择
                SelectModel = null;
                SelectModelIntroduce = null;
                //隐藏本地模型选择，下一步按钮，模型介绍页面
                IsVisibleIntroduce = false;
                
                // 从配置文件重新加载模型列表
                Task.Run(async () =>
                {
                    await GetModelsWithLoadingState();
                }).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetModels failed: {t.Exception?.Message}");
                    }
                }, TaskScheduler.Default);
            }
        }
        /// <summary>
        /// 带加载状态的模型获取
        /// </summary>
        private async Task GetModelsWithLoadingState()
        {
            try
            {
                IsLoadingModelList = true;
                await GetModels();
                IsLoadingModelList = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetModelsWithLoadingState failed: {ex.Message}");
                // 确保在异常情况下也恢复加载状态
                IsLoadingModelList = false;
            }
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
            
            // 在扫描开始前清空集合（只清空一次）
            PythonVenvPaths.Clear();
            sb.Clear();
            Outputs = string.Empty;
            
            try
            {
                IEnumerable<string> searchRoots;
                
                if (OperatingSystem.IsWindows())
                {
                    // Windows: 扫描所有固定磁盘驱动器
                    searchRoots = DriveInfo.GetDrives()
                        .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                        .Select(d => d.RootDirectory.FullName);
                }
                else
                {
                    // Linux/macOS: 只扫描主要的用户和应用目录
                    // 避免扫描虚拟文件系统挂载点（/sys, /proc 等）
                    var potentialRoots = new[] { "/home", "/Users", "/opt", "/usr/local" };
                    searchRoots = potentialRoots.Where(Directory.Exists);
                    
                    sb.AppendLine("Linux/macOS 系统，将扫描以下目录:");
                    foreach (var root in searchRoots)
                    {
                        sb.AppendLine($"  - {root}");
                    }
                    sb.AppendLine();
                    Outputs = sb.ToString();
                }

                // 在后台线程执行扫描，避免阻塞UI
                await Task.Run(() =>
                {
                    foreach (var drive in searchRoots)
                    {
                        try
                        {
                            sb.AppendLine($"正在扫描: {drive}");
                            Outputs = sb.ToString();
                            
                            // 使用同步递归方法扫描（避免死锁）
                            ScanDirectoryRecursive(drive);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "扫描目录 {Drive} 时出错", drive);
                            sb.AppendLine($"扫描 {drive} 时出错：{ex.Message}");
                            Outputs = sb.ToString();
                        }
                    }
                });
                
                sb.AppendLine($"\n扫描完成！共找到 {PythonVenvPaths.Count} 个虚拟环境");
                Outputs = sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "扫描虚拟环境时发生错误");
                await MessageBoxManager.GetMessageBoxStandard("错误", $"发生错误：{ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(MainWindow);
            }
            finally
            {
                IsScanningVenv = false;
            }
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
            string venvName = DateTime.Now.ToString("yyMMddHHmmss_Venv");
            IsEnablePythonConfigView = false;
            StringBuilder sb = new StringBuilder();
            sb.Append($"cd {venvFolder}");
            sb.Append(" && ");
            sb.Append($"python -m venv {venvName}");
            var command = sb.ToString();
            var result = await CliWrapHelper.ExecuteLine(command, onOutputReceived:HandleOutput);
            if (result.ExitCode == 0)
            {
                PythonVenvPath = Path.Combine(venvFolder, venvName);
            }
            IsEnablePythonConfigView = true;
        }
        /// <summary>
        /// 进入Venv环境，执行Pip List
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task ExecutePy()
        {
            if (string.IsNullOrEmpty(PythonVenvPath))
            {
                sb.AppendLine("请先选择或创建虚拟环境");
                EnvironmentState = "未选择虚拟环境";
                StateForeground = Brushes.Orange;
                Outputs = sb.ToString();
                return;
            }

            missingApps = [];
            //验证所填venv环境是否可用
            bool isVenvValid = await CliWrapHelper.IsVenvValid(PythonVenvPath);
            if (isVenvValid)
            {
                IsExecutingPyScript = true;
                App.PythonVenvPath = PythonVenvPath;
                var executeSb = new StringBuilder();
                var activateFile = OperatingSystem.IsWindows()? $"{PythonVenvPath}\\Scripts\\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                executeSb.Append(activateFile);
                executeSb.Append(" && ");
                executeSb.Append("pip list");
                var command = executeSb.ToString();
                var result = await CliWrapHelper.ExecuteLine(command);
                if (result.ExitCode == 0)
                {
                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        string envName = $"({PythonVenvPath.Split("\\").Last()})";
                        PipApps = result.Output.TrimStart().TrimEnd();
                    }
                }
                sb.Append(result.Output);
                Outputs = sb.ToString();
                try
                {
                    if (PipApps != null)
                    {
                        var checkResult = CheckPackages(PipApps, requireApps);
                        if (!checkResult.IsMatch)
                        {
                            EnvironmentState = "Python软件包不匹配";
                            sb.Append(checkResult.Message);
                            Outputs = sb.ToString();
                            StateForeground = Brushes.Red;
                            IsVisibleInstallMissing = true;
                            if (checkResult.MissingPackages != null)
                            {
                                missingApps.AddRange(checkResult.MissingPackages);
                            }
                        }
                        else
                        {
                            await GetModels();
                            EnvironmentState = "Python软件包已安装";
                            StateForeground = Brushes.Green;
                            IsVisibleInstallMissing = false;
                        }
                    }
                }
                catch(Exception ex)
                {
                    await MessageBoxManager.GetMessageBoxStandard("错误", $"检查环境时发生错误：{ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(MainWindow);
                }
                IsExecutingPyScript = false;
                Outputs = sb.ToString();
            }
            else
            {
                sb.AppendLine("Venv路径不可用，请重新选择或创建虚拟环境");
                EnvironmentState = "虚拟环境不可用";
                StateForeground = Brushes.Red;
                Outputs = sb.ToString();
            }
        }
        /// <summary>
        /// 安装需要的软件包（使用优化后的PackageInstaller）
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task InstallMissingApp()
        {
            if (missingApps.Count == 0)
                return;

            IsVisibleProgressBar = true;
            IsRunningProgressBar = true;

            try
            {
                // 使用新的PackageInstaller进行优化安装
                sb.AppendLine("\n=== 开始安装缺失的包 ===");
                Outputs = sb.ToString();

                // 使用优化后的安装器（带Fallback机制）
                var results = await PackageInstaller.InstallPackagesOptimizedAsync(
                    missingApps.ToList(),
                    PythonVenvPath ?? string.Empty,
                    output =>
                    {
                        sb.AppendLine(output);
                        Outputs = sb.ToString();
                    }
                );

                // 生成并显示详细报告
                var report = PackageInstaller.GenerateInstallReport(results);
                sb.AppendLine(report);
                Outputs = sb.ToString();

                // 统计结果
                var successful = results.Count(r => r.Success);
                var failed = results.Count(r => !r.Success);

                if (failed == 0)
                {
                    EnvironmentState = "Python软件包已安装";
                    StateForeground = Brushes.Green;
                    IsVisibleInstallMissing = false;
                    sb.AppendLine("\n✓ 所有包安装成功！");
                }
                else if (successful > 0)
                {
                    EnvironmentState = $"部分包安装失败 ({failed}/{results.Count})";
                    StateForeground = Brushes.Orange;
                    sb.AppendLine($"\n⚠ {failed} 个包安装失败，但核心功能可能可用");
                }
                else
                {
                    EnvironmentState = "安装失败，请手动检查";
                    StateForeground = Brushes.Red;
                    sb.AppendLine("\n✗ 所有包安装失败，请检查网络连接或手动安装");
                }

                Outputs = sb.ToString();

                // 重新检查环境
                await ExecutePy();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "安装缺失包时发生错误");
                sb.AppendLine($"\n安装过程出错: {ex.Message}");
                EnvironmentState = "安装过程出错";
                StateForeground = Brushes.Red;
                Outputs = sb.ToString();
            }
            finally
            {
                IsVisibleProgressBar = false;
                IsRunningProgressBar = false;
            }
        }
        /// <summary>
        /// 选择模型命令
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task SelectModelToShow()
        {
            if (!string.IsNullOrEmpty(SelectModel) && SelectedTaskType != null)
            {
                IsLoadingModelList = true;

                // 设置模型信息到全局配置
                App.TrainModel.PretrainedModel = SelectModel;
                // 将预训练模型名同步到自定义模型名称（用户可在参数页再修改）
                App.TrainModel.CustomModelName = SelectModel;

                // 根据任务类型选择不同的辅助脚本
                var helperScript = SelectedTaskType.Value == "detection" ? "DetectionModelHelper.py" : "ModelHelper.py";
                var fullHelperPath = Path.Combine(Environment.CurrentDirectory, "PyScripts", "Utils", helperScript);

                StringBuilder sb = new StringBuilder();
                var activateFile = OperatingSystem.IsWindows()? $"{PythonVenvPath}\\Scripts\\activate.bat" : $"source {PythonVenvPath}/bin/activate";
                sb.Append(activateFile);
                sb.Append(" && ");
                sb.Append($"python {fullHelperPath} info {SelectModel}");

                var command = sb.ToString();
                var result = await CliWrapHelper.ExecuteLine(command);

                if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.Output))
                {
                    if (result.Output.Contains("###ModelInfo###"))
                    {
                        var scriptModelInfo = result.Output.Split("###ModelInfo###")[1].TrimStart().TrimEnd();
                        SelectModelIntroduce = scriptModelInfo;
                        IsVisibleIntroduce = true;
                    }
                }

                IsLoadingModelList = false;
            }
        }
        /// <summary>
        /// 选择本地模型命令
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task SelectLocalModel()
        {
            var thisWindow = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var toplevel = TopLevel.GetTopLevel(thisWindow?.MainWindow);
            FilePickerFileType onnxAll = new("All Pytorch")
            {
                Patterns = ["*.pth","*.pt"]
            };
            if (toplevel != null)
            {
                var model = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = $"请选择{SelectModel}类型的pytorch模型",
                    AllowMultiple = false,
                    FileTypeFilter = [onnxAll]
                });
                if (model.Count>0)
                {
                    var modelPath = model[0].Path.LocalPath;
                    
                    // 验证模型一致性
                    if (!string.IsNullOrEmpty(SelectModel) && !string.IsNullOrEmpty(App.PythonVenvPath))
                    {
                        var validationResult = await CliWrapHelper.ValidateModelConsistencyAsync(
                            modelPath, SelectModel, App.PythonVenvPath);
                        
                        if (!validationResult.valid)
                        {
                            // 显示警告或错误对话框
                            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                                "模型验证",
                                validationResult.message,
                                validationResult.modelName == null ? ButtonEnum.Ok : ButtonEnum.YesNo,
                                Icon.Warning);
                            
                            var result = await messageBox.ShowAsync();
                            
                            // 如果是严重错误（模型不匹配）且用户选择No，则取消选择
                            if (validationResult.modelName != null && result == ButtonResult.No)
                            {
                                return;
                            }
                        }
                    }
                    
                    SelectedLocalWeightText = model[0].Name;
                    App.TrainModel.LocalWeightsPath = modelPath;
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
        /// <summary>
        /// 运行环境诊断
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task RunDiagnostics()
        {
            IsRunningDiagnostics = true;
            ShowDiagnosticReport = true;

            try
            {
                sb.Clear();
                sb.AppendLine("正在运行环境诊断...\n");
                Outputs = sb.ToString();

                // 运行诊断
                var report = await EnvironmentDiagnostics.CheckEnvironment(PythonPath);
                _cachedDiagnosticReport = report;

                // 格式化报告
                var formattedReport = EnvironmentDiagnostics.FormatReport(report);
                sb.AppendLine(formattedReport);

                // 如果有CUDA，显示推荐的PyTorch安装命令
                if (report.HasCUDA)
                {
                    var torchCommand = EnvironmentDiagnostics.GetRecommendedPyTorchInstallCommand(report);
                    sb.AppendLine("\n推荐的PyTorch安装命令:");
                    sb.AppendLine($"  {torchCommand}");
                }

                // 检查是否应该使用预编译wheel
                if (EnvironmentDiagnostics.ShouldUsePrecompiledWheel(report))
                {
                    sb.AppendLine("\n建议: 优先使用预编译wheel以避免编译问题");
                }

                Outputs = sb.ToString();
                DiagnosticReport = formattedReport;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "运行环境诊断时发生错误");
                sb.AppendLine($"\n诊断过程出错: {ex.Message}");
                Outputs = sb.ToString();
            }
            finally
            {
                IsRunningDiagnostics = false;
            }
        }
        #endregion

        public class PackageInfo
        {
            public string? Name { get; set; }
            public string? Version { get; set; }
        }

        public class CheckResult
        {
            public bool IsMatch { get; set; }
            public List<string>? MissingPackages { get; set; }
            public string? Message { get; set; }
        }

        // 任务类型信息
        public class TaskTypeInfo
        {
            public TaskTypeInfo()
            {
                Name = string.Empty;
                Value = string.Empty;
            }

            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}
