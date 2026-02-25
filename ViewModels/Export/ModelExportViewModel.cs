using AutoTrainer.Helpers;
using AutoTrainer.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Notification;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    #region 转换任务模型
    public partial class ConversionTask : ObservableObject
    {
        [ObservableProperty] private string formatName = string.Empty;
        [ObservableProperty] private string displayName = string.Empty;
        [ObservableProperty] private string statusText = "等待中";
        [ObservableProperty] private double progress = 0;
        [ObservableProperty] private string outputPath = string.Empty;
        [ObservableProperty] private string logText = "等待开始转换...";
        [ObservableProperty] private bool isEnabled = true;
        [ObservableProperty] private string badgeColor = "#4CAF50";
        [ObservableProperty] private string progressColor = "#4CAF50";
        
        public string FileExtension { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
    #endregion

    public partial class ModelExportViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private Stopwatch _stopwatch = new Stopwatch();

        #region 可绑定属性
        
        // 通知管理器
        public INotificationMessageManager NotifyManager { get; } = new NotificationMessageManager();
        
        [ObservableProperty]
        private string modelPath = string.Empty;
        
        [ObservableProperty]
        private string modelName = string.Empty;
        
        [ObservableProperty]
        private bool isModelNameReadOnly = true;  // 模型名称只读
        
        [ObservableProperty]
        private string modelNameStatus = "未加载模型";  // 模型名称状态提示
        
        [ObservableProperty]
        private string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
        
        [ObservableProperty]
        private int opsetVersion = 2; // Index: 0=11, 1=13, 2=15
        
        [ObservableProperty]
        private bool optimizeModel = true;
        
        [ObservableProperty]
        private bool autoValidate = false;
        
        // 输入尺寸
        [ObservableProperty]
        private string batchSize = "1";
        
        [ObservableProperty]
        private string channels = "3";
        
        [ObservableProperty]
        private string imageSize = "224";
        
        [ObservableProperty]
        private bool includePreprocessing = false;
        
        [ObservableProperty]
        private bool dynamicInputSize = false;
        
        // 转换状态
        [ObservableProperty]
        private bool isConverting = false;
        
        [ObservableProperty]
        private string conversionStatus = "准备就绪";
        
        [ObservableProperty]
        private string completedCount = "0/0";
        
        [ObservableProperty]
        private string successFailCount = "0/0";
        
        [ObservableProperty]
        private string elapsedTime = "0s";
        
        [ObservableProperty]
        private string outputFileCount = "0 个";
        
        // 转换任务集合
        [ObservableProperty]
        private ObservableCollection<ConversionTask> conversionTasks = new();

        #endregion

        public ModelExportViewModel()
        {
            Log.Information("ModelExportViewModel 初始化");
            InitializeConversionTasks();
            
            // 确保输出目录存在
            if (!Directory.Exists(OutputDirectory))
            {
                try
                {
                    Directory.CreateDirectory(OutputDirectory);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创建输出目录失败: {OutputDirectory}", OutputDirectory);
                }
            }
        }

        /// <summary>
        /// 初始化转换任务列表
        /// </summary>
        private void InitializeConversionTasks()
        {
            ConversionTasks = new ObservableCollection<ConversionTask>
            {
                new ConversionTask
                {
                    FormatName = "onnx",
                    DisplayName = "ONNX 格式转换",
                    FileExtension = ".onnx",
                    IsSelected = true,
                    BadgeColor = "#4CAF50",
                    ProgressColor = "#4CAF50"
                },
                new ConversionTask
                {
                    FormatName = "torchscript",
                    DisplayName = "TorchScript 格式转换",
                    FileExtension = ".pt",
                    IsSelected = true,
                    BadgeColor = "#2196F3",
                    ProgressColor = "#2196F3"
                },
                new ConversionTask
                {
                    FormatName = "tensorrt",
                    DisplayName = "TensorRT 格式转换 (NVIDIA GPU)",
                    FileExtension = ".engine",
                    IsSelected = false,
                    BadgeColor = "#76B900",
                    ProgressColor = "#76B900"
                },
                new ConversionTask
                {
                    FormatName = "openvino",
                    DisplayName = "OpenVINO 格式转换 (Intel)",
                    FileExtension = ".xml",
                    IsSelected = false,
                    BadgeColor = "#0071C5",
                    ProgressColor = "#0071C5"
                },
                new ConversionTask
                {
                    FormatName = "coreml",
                    DisplayName = "Core ML 格式转换 (Apple)",
                    FileExtension = ".mlmodel",
                    IsSelected = false,
                    BadgeColor = "#555555",
                    ProgressColor = "#555555"
                }
            };
        }

        /// <summary>
        /// 选择模型文件
        /// </summary>
        [RelayCommand]
        private async Task BrowseModelFile()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow as Control : null;
            
            if (mainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(mainWindow);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择模型权重文件",
                        AllowMultiple = false,
                        FileTypeFilter = new[] 
                        { 
                            new FilePickerFileType("PyTorch Models") { Patterns = new[] { "*.pth", "*.pt" } },
                            new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                        }
                    });
                    
                    if (files.Count > 0)
                    {
                        ModelPath = files[0].TryGetLocalPath() ?? string.Empty;
                        
                        // 从模型 metadata 中读取 model_name
                        if (!string.IsNullOrEmpty(ModelPath))
                        {
                            await LoadModelNameFromMetadata();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 选择输出目录
        /// </summary>
        [RelayCommand]
        private async Task BrowseOutputDirectory()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow as Control : null;
            
            if (mainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(mainWindow);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "选择输出目录",
                        AllowMultiple = false
                    });
                    
                    if (folders.Count > 0)
                    {
                        OutputDirectory = folders[0].TryGetLocalPath() ?? string.Empty;
                        UpdateOutputPaths();
                    }
                }
            }
        }

        /// <summary>
        /// 从模型 metadata 中加载模型名称
        /// </summary>
        private async Task LoadModelNameFromMetadata()
        {
            try
            {
                ModelNameStatus = "正在读取模型信息...";
                Log.Information("从模型文件读取 metadata: {ModelPath}", ModelPath);

                // 检查 Python 虚拟环境
                if (string.IsNullOrEmpty(App.PythonVenvPath) || !Directory.Exists(App.PythonVenvPath))
                {
                    ModelNameStatus = "错误：未配置 Python 环境";
                    Log.Warning("Python 虚拟环境未配置，无法读取模型 metadata");
                    
                    await MessageBoxManager.GetMessageBoxStandard(
                        "警告",
                        "未配置 Python 虚拟环境，无法读取模型信息。\n请先在模型仓库页配置 Python 环境。",
                        MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Warning
                    ).ShowWindowDialogAsync(MainWindow);
                    return;
                }

                // 使用 CliWrapHelper 读取模型 metadata
                var modelName = await CliWrapHelper.GetModelNameFromMetadataAsync(ModelPath, App.PythonVenvPath);

                if (!string.IsNullOrEmpty(modelName))
                {
                    ModelName = modelName;
                    ModelNameStatus = $"✓ 模型: {modelName}";
                    Log.Information("成功读取模型名称: {ModelName}", modelName);
                    
                    // 更新输出路径
                    UpdateOutputPaths();
                }
                else
                {
                    ModelName = string.Empty;
                    ModelNameStatus = "✗ 非本软件训练的模型";
                    Log.Warning("无法读取模型 metadata，该模型可能不是本软件训练的");
                    
                    await MessageBoxManager.GetMessageBoxStandard(
                        "模型信息",
                        "无法从模型文件中读取 model_name 元数据。\n\n" +
                        "这可能不是本软件训练的模型，或模型文件格式不正确。\n" +
                        "本软件训练的模型会在 metadata 中保存标准架构名称（如 mobilenet_v3_large）。",
                        MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Info
                    ).ShowWindowDialogAsync(MainWindow);
                }
            }
            catch (Exception ex)
            {
                ModelName = string.Empty;
                ModelNameStatus = "✗ 读取失败";
                Log.Error(ex, "读取模型 metadata 时发生错误");
                
                await MessageBoxManager.GetMessageBoxStandard(
                    "错误",
                    $"读取模型信息时发生错误：\n{ex.Message}",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error
                ).ShowWindowDialogAsync(MainWindow);
            }
        }

        /// <summary>
        /// 更新所有任务的输出路径
        /// </summary>
        private void UpdateOutputPaths()
        {
            if (string.IsNullOrEmpty(OutputDirectory))
                return;

            // 使用原始模型文件名（不包含扩展名）
            string baseFileName = string.IsNullOrEmpty(ModelPath) 
                ? "model" 
                : Path.GetFileNameWithoutExtension(ModelPath);

            foreach (var task in ConversionTasks)
            {
                task.OutputPath = Path.Combine(OutputDirectory, baseFileName + task.FileExtension);
            }
        }

        /// <summary>
        /// 开始转换
        /// </summary>
        [RelayCommand]
        private async Task StartConversion()
        {
            Log.Information("开始模型转换流程");

            // 输入验证
            if (!await ValidateInputs())
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            IsConverting = true;
            ConversionStatus = "转换中";
            _stopwatch.Restart();

            var selectedTasks = ConversionTasks.Where(t => t.IsSelected).ToList();
            int totalTasks = selectedTasks.Count;
            int completedTasks = 0;
            int successTasks = 0;
            int failedTasks = 0;

            try
            {
                // 更新输出路径
                UpdateOutputPaths();

                // 按顺序执行每个转换任务
                foreach (var task in selectedTasks)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    Log.Information("开始转换格式: {Format}", task.FormatName);
                    
                    task.StatusText = "转换中";
                    task.Progress = 0;
                    task.LogText = $"正在转换到 {task.DisplayName}...\n";

                    bool success = await ConvertToFormat(task, _cancellationTokenSource.Token);
                    
                    completedTasks++;
                    if (success)
                    {
                        successTasks++;
                        task.StatusText = "完成";
                        task.Progress = 100;
                        task.LogText += $"\n✓ 转换成功！\n输出文件: {task.OutputPath}";
                    }
                    else
                    {
                        failedTasks++;
                        task.StatusText = "失败";
                        task.Progress = 0;
                    }

                    // 更新统计信息
                    CompletedCount = $"{completedTasks}/{totalTasks}";
                    SuccessFailCount = $"{successTasks}/{failedTasks}";
                    ElapsedTime = $"{_stopwatch.Elapsed.TotalSeconds:F1}s";
                    OutputFileCount = $"{successTasks} 个";
                }

                _stopwatch.Stop();
                IsConverting = false;
                ConversionStatus = completedTasks == successTasks ? "转换完成" : "部分失败";
                
                Log.Information("转换流程完成。成功: {Success}, 失败: {Failed}", successTasks, failedTasks);

                // 显示完成通知
                if (successTasks > 0)
                {
                    NotifyManager.ShowSuccessWithDirectory($"成功转换 {successTasks} 个格式", OutputDirectory, 5);
                }
                
                if (failedTasks > 0)
                {
                    NotifyManager.ShowWarning($"{failedTasks} 个格式转换失败，请查看日志", 5);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转换过程中发生错误");
                ConversionStatus = "转换失败";
                IsConverting = false;
                
                await MessageBoxManager.GetMessageBoxStandard(
                    "转换失败",
                    $"转换过程中发生错误:\n{ex.Message}",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error
                ).ShowWindowDialogAsync(MainWindow);
            }
        }

        /// <summary>
        /// 取消转换
        /// </summary>
        [RelayCommand]
        private void CancelConversion()
        {
            Log.Information("用户取消转换");
            _cancellationTokenSource?.Cancel();
            ConversionStatus = "已取消";
            IsConverting = false;
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private async Task<bool> ValidateInputs()
        {
            var mainWindow = MainWindow;
            
            if (string.IsNullOrEmpty(ModelPath) || !File.Exists(ModelPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请选择一个有效的模型权重文件").ShowWindowDialogAsync(mainWindow);
                return false;
            }

            if (string.IsNullOrEmpty(ModelName))
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "错误", 
                    "无法获取模型架构名称。\n\n" +
                    "该模型可能不是本软件训练的。本软件训练的模型会在 metadata 中保存标准架构名称。"
                ).ShowWindowDialogAsync(mainWindow);
                return false;
            }

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请选择输出目录").ShowWindowDialogAsync(mainWindow);
                return false;
            }

            if (string.IsNullOrEmpty(App.PythonVenvPath) || !Directory.Exists(App.PythonVenvPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请先在模型仓库页配置一个有效的Python虚拟环境").ShowWindowDialogAsync(mainWindow);
                return false;
            }

            if (!ConversionTasks.Any(t => t.IsSelected))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请至少选择一个导出格式").ShowWindowDialogAsync(mainWindow);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行单个格式的转换
        /// </summary>
        private async Task<bool> ConvertToFormat(ConversionTask task, CancellationToken cancellationToken)
        {
            try
            {
                // 确保输出目录存在
                var outputDir = Path.GetDirectoryName(task.OutputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 创建转换配置文件
                var configPath = Path.Combine(Path.GetTempPath(), $"conversion_config_{Guid.NewGuid()}.json");
                
                var opsetVersionValue = OpsetVersion switch
                {
                    0 => 11,
                    1 => 13,
                    2 => 15,
                    _ => 15
                };

                var inputShape = new[] { 
                    int.Parse(BatchSize), 
                    int.Parse(Channels), 
                    int.Parse(ImageSize), 
                    int.Parse(ImageSize) 
                };
                
                // 创建转换配置
                var config = new
                {
                    model_path = ModelPath,
                    model_name = ModelName,
                    output_path = task.OutputPath.Replace(task.FileExtension, ""),
                    input_shape = inputShape,
                    opset_version = opsetVersionValue,
                    enable_dynamic_axes = DynamicInputSize,  // 根据UI选项决定是否启用动态轴
                    optimize_model = OptimizeModel  // 是否优化模型
                };

                // 保存配置文件
                var configJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(configPath, configJson);

                Log.Debug("转换配置已保存到: {ConfigPath}", configPath);

                // Python脚本路径
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PyScripts", "Conversion", "convert_model.py");
                
                var arguments = $"--config \"{configPath}\" --format \"{task.FormatName}\"";

                Log.Debug("执行转换命令: python {Script} {Arguments}", scriptPath, arguments);

                task.Progress = 10;
                task.LogText += $"正在加载模型...\n";

                var result = await CliWrapHelper.ExecutePythonScriptWithStreamingAsync(
                    scriptPath,
                    App.PythonVenvPath,
                    arguments,
                    onStdoutLine: (output) =>
                    {
                        task.LogText += output + "\n";
                        
                        // 根据输出更新进度
                        if (output.Contains("加载") || output.Contains("加载模型"))
                            task.Progress = 30;
                        else if (output.Contains("准备") || output.Contains("正在准备"))
                            task.Progress = 50;
                        else if (output.Contains("转换") || output.Contains("开始转换"))
                            task.Progress = 70;
                        else if (output.Contains("成功") || output.Contains("完成"))
                            task.Progress = 90;
                    },
                    cancellationToken: cancellationToken
                );

                // 清理临时配置文件
                try
                {
                    if (File.Exists(configPath))
                        File.Delete(configPath);
                }
                catch { }

                task.Progress = 100;

                if (result.ExitCode == 0)
                {
                    Log.Information("格式 {Format} 转换成功", task.FormatName);
                    return true;
                }
                else
                {
                    Log.Warning("格式 {Format} 转换失败，退出码: {ExitCode}", task.FormatName, result.ExitCode);
                    task.LogText += $"\n✗ 转换失败 (退出码: {result.ExitCode})\n";
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        task.LogText += $"错误信息: {result.Error}\n";
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转换格式 {Format} 时发生异常", task.FormatName);
                task.LogText += $"\n✗ 异常: {ex.Message}\n";
                return false;
            }
        }
    }
}
