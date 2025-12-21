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

namespace AutoTrainer.ViewModels
{
    public partial class ModelConfigViewModel : ViewModelBase
    {
        #region 构造函数
        public ModelConfigViewModel()
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
        private readonly HashSet<string> excludedPaths =
        [
            @"Windows",
            @"Documents and Settings",
            @"Program Files",
            @"Program Files (x86)",
            @"ProgramData",
            @"System Volume Information",
            @"$RECYCLE.BIN"
        ];
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
        private string? scanningFolder = string.Empty;

        // 任务类型选择相关
        [ObservableProperty]
        private ObservableCollection<TaskTypeInfo> taskTypes = new();

        [ObservableProperty]
        private TaskTypeInfo? selectedTaskType;

        // 任务类型切换状态
        [ObservableProperty]
        private bool isTaskTypeChanging = false;

        #endregion

        #region 函数
        /// <summary>
        /// 设置需求包显示
        /// </summary>
        /// <returns></returns>
        private async Task SetRequirementsDisplay()
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
                    var cudaVersion = match.Groups[1].Value;
                    string cuVersion = cudaVersion.Trim().Replace(".", "");
                    if (!string.IsNullOrEmpty(cuVersion))
                    {
                        torchPackages.Add($"torch --index-url https://download.pytorch.org/whl/{cuVersion}");
                        torchPackages.Add($"torchvision --index-url https://download.pytorch.org/whl/{cuVersion}");
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
            var command = OperatingSystem.IsWindows() ? "where python3" : "which python3";
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
            if (path.StartsWith("C:\\Users\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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
                            PythonVenvPaths.Add(dir);
                            PythonVenvPath ??= dir;
                            Outputs = sb.ToString();
                        }
                        ScanDirectory(dir).Wait();
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }
                catch (Exception ex)
                {
                    await MessageBoxManager.GetMessageBoxStandard("扫描识别", $"警告：扫描目录 {path} 时出错：{ex.Message}\n", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(MainWindow);
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
                // 解析已安装的包
                var installedPackages = installedPackagesStr
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => ParsePackageLine(line))
                    .Where(pkg => !string.IsNullOrEmpty(pkg.Name))
                    .ToDictionary(pkg => pkg.Name!, pkg => pkg.Version);

                var requiredPackageNames = new HashSet<string>();

                // 检查所需的包
                foreach (var requiredPackageLine in requiredPackages)
                {
                    if (string.IsNullOrWhiteSpace(requiredPackageLine))
                        continue;

                    var packageName = requiredPackageLine.Split(new[] { '=', '>', '<', ' ' }, 2)[0].Trim().ToLowerInvariant();

                    if (requiredPackageNames.Contains(packageName))
                        continue;

                    if (!installedPackages.ContainsKey(packageName))
                    {
                        result.IsMatch = false;
                        if (packageName == "torch" || packageName == "torchvision")
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
                    requiredPackageNames.Add(packageName);
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
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed)
                    .Select(d => d.RootDirectory.FullName);

                foreach (var drive in drives)
                {
                    try
                    {
                        PythonVenvPaths = [];
                        await ScanDirectory(drive);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard("扫描识别",$"扫描驱动器 {drive} 时出错：{ex.Message}\n",MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(MainWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard($"发生错误：{ex.Message}", "错误").ShowWindowDialogAsync(MainWindow);
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
            string venvName = DateTime.Now.ToString("yyMMddHHmmss_Venv");
            IsEnablePythonConfigView = false;
            StringBuilder sb = new StringBuilder();
            sb.Append($"cd {venvFolder}");
            sb.Append(" && ");
            sb.Append($"python3 -m venv {venvName}");
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
            ThemeManager.Toggle();
            if (IsCheckedVenv && !string.IsNullOrEmpty(PythonVenvPath))
            {
                missingApps = [];
                //验证所填venv环境是否可用
                bool isVenvValid = await CliWrapHelper.IsVenvValid(PythonVenvPath);
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
                IsVisibleProgressBar = true;
                IsRunningProgressBar = true;

                try
                {
                    var activateFile = OperatingSystem.IsWindows() ? $@"{PythonVenvPath}\Scripts\activate.bat" : $"source {PythonVenvPath}/bin/activate";

                    var isTorchMissing = missingApps.Remove("torch");

                    if (isTorchMissing)
                    {
                        var cudaVersionResult = await CliWrapHelper.ExecuteLine("nvidia-smi");
                        string torchInstallCommand = "pip3 install torch torchvision"; 

                        if (cudaVersionResult.ExitCode == 0 && !string.IsNullOrEmpty(cudaVersionResult.Output))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(cudaVersionResult.Output, @"CUDA Version:\s*(\d+\.\d+)");
                            if (match.Success)
                            {
                                string cudaVersion = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(cudaVersion)) 
                                { 
                                    string cuVersion = cudaVersion.Trim().Replace(".", "");
                                    torchInstallCommand = $"pip3 install torch torchvision --index-url https://download.pytorch.org/whl/{cuVersion}";
                                }
                            }
                        }

                        StringBuilder torchCommandSb = new StringBuilder();
                        torchCommandSb.Append(activateFile);
                        torchCommandSb.Append($" && {torchInstallCommand}");
                        await CliWrapHelper.ExecuteLine(torchCommandSb.ToString(), isShowTerminal: false, onOutputReceived: HandleOutput);
                    }

                    if (missingApps.Count > 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(activateFile);
                        foreach (var missingApp in missingApps)
                        {
                            sb.Append($" && pip3 install \"{missingApp}\"");
                        }
                        var command = sb.ToString();
                        await CliWrapHelper.ExecuteLine(command, isShowTerminal: false, onOutputReceived: HandleOutput);
                    }

                    await ExecutePy();
                }
                finally
                {
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
