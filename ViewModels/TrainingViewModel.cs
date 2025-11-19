using AutoTrainer.Helpers;
using AutoTrainer.Models;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using Newtonsoft.Json;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class TrainingViewModel : ViewModelBase
    {
        private bool isPyRunning = false;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly CancellationTokenSource _refreshCts = new();
        private int ScanningIndex = 0;
        public TrainingViewModel()
        {
            Log.Information("TrainingViewModel 初始化完成");
            try
            {
                InitialPlot();
                Log.Debug("TrainingViewModel 初始化成功完成");

                // 启动性能监控循环
                _ = RefreshSystemInfo();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TrainingViewModel 初始化失败");
                throw;
            }
        }

        #region 可绑定属性
        /// <summary>
        /// 图表线段
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ISeries>? series;
        /// <summary>
        /// 训练准确度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue>? trainAccValues;
        /// <summary>
        /// 训练损失度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue>? trainLossValues;
        /// <summary>
        /// 验证准确度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue>? validationAccValues;
        /// <summary>
        /// 验证损失度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue>? validationLossValues;
        /// <summary>
        /// 是否显示下一步
        /// </summary>
        [ObservableProperty]
        private bool isShowNextPage = false;
        /// <summary>
        /// 模型配置参数
        /// </summary>
        [ObservableProperty]
        private string? modelParamStr;
        /// <summary>
        /// 训练执行输出
        /// </summary>
        [ObservableProperty]
        private string? pyOutput;
        /// <summary>
        /// 每轮状态
        /// </summary>
        [ObservableProperty]
        private EpochState epochState = new();
        /// <summary>
        /// CPU占用率
        /// </summary>
        [ObservableProperty]
        private string cPURate;
        /// <summary>
        /// GPU占用率
        /// </summary>
        [ObservableProperty]
        private string gPURate;
        /// <summary>
        /// RAM占用率
        /// </summary>
        [ObservableProperty]
        private string rAMRate;

        public ICartesianAxis[] XAxes { get; set; } = [
            new Axis
            {
                Name = "Epoch",
                MinStep = 1
            }
        ];

        public ICartesianAxis[] YAxes { get; set; } = [
            new Axis
            {
                Name = "Accuracy & Loss Rate",
            }
        ];
        #endregion

        #region 命令
        /// <summary>
        /// 加载训练参数信息
        /// </summary>
        [RelayCommand]
        private void LoadParamInfo()
        {
            Log.Information("加载训练参数信息");
            try
            {
                var sb = new StringBuilder();
                var modelParam = App.TrainModel;

                if (modelParam == null)
                {
                    Log.Warning("训练模型参数为空，无法加载参数信息");
                    return;
                }

                Log.Debug("为模型加载参数: {Model}, 任务类型: {TaskType}", modelParam.PretrainedModel, modelParam.TaskType);

                // 通用参数（所有任务类型都显示）
                sb.AppendLine("=== 通用训练参数 ===");
                sb.AppendLine("任务类型: " + (modelParam.TaskType == "classification" ? "图像分类" : "目标检测"));
                sb.AppendLine("模型: " + modelParam.PretrainedModel);
                sb.AppendLine("学习率: " + modelParam.LearningRate);
                sb.AppendLine("优化器: " + modelParam.Optimizer);
                sb.AppendLine("学习率调度器: " + modelParam.LrScheduler);
                sb.AppendLine("权重衰减: " + modelParam.WeightDecay);
                sb.AppendLine("批量大小: " + modelParam.BatchSize);
                sb.AppendLine("训练轮数: " + modelParam.Epochs);
                sb.AppendLine("早停轮数: " + modelParam.EarlyStoppingRounds);
                sb.AppendLine("早停阈值: " + modelParam.EarlyStoppingDelta);
                sb.AppendLine("本地权重路径: " + (string.IsNullOrEmpty(modelParam.LocalWeightsPath) ? "无" : modelParam.LocalWeightsPath));
                sb.AppendLine("");

                if (modelParam.TaskType == "classification")
                {
                    // 分类任务专用参数
                    sb.AppendLine("=== 分类任务专用参数 ===");
                    sb.AppendLine("验证集比例: " + modelParam.ClassifyValidImagesSplit);
                    sb.AppendLine("训练数据路径: " + modelParam.ClassifyTrainImagesPath);
                    sb.AppendLine("验证数据路径: " + modelParam.ClassifyValidImagesPath);

                    // 数据增强参数
                    sb.AppendLine("数据增强设置:");
                    sb.AppendLine("  - 随机水平翻转: " + (modelParam.RandomHorizonFlipChecked ? "启用" : "禁用"));
                    sb.AppendLine("  - 随机垂直翻转: " + (modelParam.RandomVerticalFlipChecked ? "启用" : "禁用"));
                    sb.AppendLine("  - 随机旋转: " + (modelParam.RandomRotationChecked ? "启用" : "禁用"));
                    sb.AppendLine("  - 随机缩放: " + (modelParam.RandomZoomChecked ? "启用" : "禁用"));
                    sb.AppendLine("  - 随机亮度: " + (modelParam.RandomBrightnessChecked ? "启用" : "禁用"));
                    sb.AppendLine("  - 随机对比度: " + (modelParam.RandomContrastChecked ? "启用" : "禁用"));

                    // 损失函数配置
                    if (modelParam.LossFunction != null)
                    {
                        sb.AppendLine("损失函数配置:");
                        sb.AppendLine("  - 类型: " + modelParam.LossFunction.type);
                        if (modelParam.LossFunction.args != null)
                        {
                            if (modelParam.LossFunction.args.reduction != null)
                                sb.AppendLine("  - 计算方式: " + modelParam.LossFunction.args.reduction);
                            if (modelParam.LossFunction.args.label_smoothing.HasValue)
                                sb.AppendLine("  - 标签平滑: " + modelParam.LossFunction.args.label_smoothing);
                            if (modelParam.LossFunction.args.Beta.HasValue)
                                sb.AppendLine("  - Beta参数: " + modelParam.LossFunction.args.Beta);
                        }
                    }
                }
                else if (modelParam.TaskType == "detection")
                {
                    // 检测任务专用参数
                    sb.AppendLine("=== 检测任务专用参数 ===");
                    sb.AppendLine("标注格式: " + modelParam.AnnotationFormat);
                    sb.AppendLine("训练图像路径: " + modelParam.TrainImagesPath);
                    sb.AppendLine("训练标注文件: " + modelParam.TrainAnnotationPath);
                    sb.AppendLine("验证图像路径: " + modelParam.ValImagesPath);
                    sb.AppendLine("验证标注文件: " + modelParam.ValAnnotationPath);

                    // 检测损失函数配置
                    if (modelParam.DetectionLoss != null)
                    {
                        sb.AppendLine("检测损失函数配置:");
                        sb.AppendLine("  - RPN分类权重: " + modelParam.DetectionLoss.RpnClassificationWeight);
                        sb.AppendLine("  - RPN回归权重: " + modelParam.DetectionLoss.RpnBoxRegressionWeight);
                        sb.AppendLine("  - ROI分类权重: " + modelParam.DetectionLoss.RoIClassificationWeight);
                        sb.AppendLine("  - ROI回归权重: " + modelParam.DetectionLoss.RoIBoxRegressionWeight);

                        if (modelParam.DetectionLoss.FocalLossAlpha.HasValue)
                            sb.AppendLine("  - Focal Loss Alpha: " + modelParam.DetectionLoss.FocalLossAlpha);
                        if (modelParam.DetectionLoss.FocalLossGamma.HasValue)
                            sb.AppendLine("  - Focal Loss Gamma: " + modelParam.DetectionLoss.FocalLossGamma);

                        sb.AppendLine("  - IoU损失类型: " + modelParam.DetectionLoss.IouLossType);
                        sb.AppendLine("  - 掩码权重: " + modelParam.DetectionLoss.MaskWeight);
                        sb.AppendLine("  - 关键点权重: " + modelParam.DetectionLoss.KeypointWeight);
                    }
                }

                // 通用输出参数
                sb.AppendLine("=== 输出配置 ===");
                sb.AppendLine("模型保存路径: " + modelParam.ModelOutputPath);
                sb.AppendLine("训练日志输出路径: " + modelParam.PyTrainLogOutputPath);

                EpochState.TotalEpochs = modelParam.Epochs;
                ModelParamStr = sb.ToString();

                Log.Information("训练参数加载成功。任务类型: {TaskType}, 轮数: {Epochs}, 批量大小: {BatchSize}, 模型: {Model}",
                    modelParam.TaskType, modelParam.Epochs, modelParam.BatchSize, modelParam.PretrainedModel);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载训练参数信息失败");
                throw;
            }
        }
        /// <summary>
        /// 开始训练
        /// </summary>
        [RelayCommand]
        private async Task StartTraining()
        {
            Log.Information("开始训练过程");
            try
            {
                if (App.TrainModel == null)
                {
                    Log.Error("无法开始训练: TrainModel 为空");
                    await MessageBoxManager.GetMessageBoxStandard("训练失败", "训练模型配置为空", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                    return;
                }

                // 根据任务类型选择不同的训练流程
                if (App.TrainModel.TaskType == "detection")
                {
                    await StartDetectionTraining();
                }
                else
                {
                    await StartClassificationTraining();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动或完成训练过程失败");
                isPyRunning = false;
                await MessageBoxManager.GetMessageBoxStandard("训练失败", $"训练过程中发生错误: {ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
            }
        }
        /// <summary>
        /// 转到下一页
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task GotoNextPage() // Removed UserControl o parameter
        {
            Log.Debug("训练完成后导航到下一页");
            try
            {
                if (App.TrainModel == null)
                {
                    Log.Warning("App.TrainModel 为空，无法设置验证页面初始状态");
                    await MessageBoxManager.GetMessageBoxStandard("错误", "训练模型配置为空，无法导航到验证页面").ShowWindowAsync();
                    return;
                }

                // Pass training results to ValidationViewModel
                await App.ValidationVM.SetInitialStateFromTraining(App.TrainModel);
                Log.Information("成功设置验证页面初始状态");

                // Navigate to the validation tab
                App.MainVM.SelectTabIndex = 4; // Assuming index 4 is the ValidModelPerformanceView tab
                Log.Information("成功导航到验证页面（标签索引 4）");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导航到下一页失败");
                await MessageBoxManager.GetMessageBoxStandard("严重错误", $"导航到验证页面失败: {ex.Message}").ShowWindowAsync();
            }
        }
        #endregion

        #region 函数
        /// <summary>
        /// 刷新系统硬件信息，CPU,GPU,RAM
        /// </summary>
        /// <returns></returns>
        private async Task RefreshSystemInfo()
        {
            while (true)
            {
                const int intervalMs = 3000; // 3 秒刷新一次

                while (!_refreshCts.IsCancellationRequested)
                {
                    try
                    {
                        // 并行获取三项指标，减少总耗时
                        var cpuTask = GetCpuRateAsync();
                        var gpuTask = GetGpuRateAsync();
                        var ramTask = GetRamRateAsync();

                        await Task.WhenAll(cpuTask, gpuTask, ramTask);

                        // 写入 ObservableProperty（UI 自动更新）
                        CPURate = (await cpuTask).ToString() + "%";
                        GPURate = (await gpuTask).ToString() + "%";
                        RAMRate = (await ramTask).ToString() + "%";
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "获取系统信息时出现异常，继续下一次循环");
                    }

                    // 等待下一轮
                    try
                    {
                        await Task.Delay(intervalMs, _refreshCts.Token);
                    }
                    catch (TaskCanceledException) { break; }
                }
            }
        }
        /// <summary>
        /// 获取CPU占用率
        /// </summary>
        /// <returns></returns>
        private static async Task<int> GetCpuRateAsync()
        {
            // Windows: PowerShell Get-Counter (实时 CPU %)
            // Linux/macOS: 使用 top -bn1
            string cmd;
            if (OperatingSystem.IsWindows())
            {
                cmd = @"powershell.exe -Command ""(Get-Counter '\Processor(_Total)\% Processor Time').CounterSamples.CookedValue""";
            }
            else
            {
                cmd = @"top -bn1 | grep '%Cpu' | awk '{print $2}' | cut -d',' -f1";  // 只取 user %，避免逗号
            }

            var result = await CliWrapHelper.ExecuteLine(cmd);
            if (result.ExitCode != 0) return 0;

            string output = result.Output?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(output)) return 0;

            // Windows: 直接浮点数，如 12.345
            // Linux/macOS: 12.3
            if (float.TryParse(output, out var percent))
                return (int)Math.Round(percent);

            return 0;
        }
        /// <summary>
        /// 获取RAM占用率
        /// </summary>
        /// <returns></returns>
        private static async Task<int> GetRamRateAsync()
        {
            string cmd;
            if (OperatingSystem.IsWindows())
            {
                // PowerShell: 计算 (Total - Free) / Total * 100
                cmd = @"powershell.exe -Command ""$total = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory; $free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory * 1KB; [math]::Round(100 * (1 - $free / $total), 0)""";
            }
            else
            {
                // Linux/macOS: free -m，计算 used / total * 100
                cmd = @"free -m | awk 'NR==2{printf ""%d"", $3*100/$2}'";
            }

            var result = await CliWrapHelper.ExecuteLine(cmd);
            if (result.ExitCode != 0) return 0;

            string output = result.Output?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(output)) return 0;

            if (int.TryParse(output, out var percent))
                return Math.Max(0, Math.Min(100, percent));  // 限制 0-100

            return 0;
        }
        /// <summary>
        /// 获取GPU占用率
        /// </summary>
        /// <returns></returns>
        private static async Task<int> GetGpuRateAsync()
        {
            // 只在检测到 nvidia-smi 时尝试
            string checkCmd = OperatingSystem.IsWindows()
                ? "where nvidia-smi"
                : "which nvidia-smi";

            var check = await CliWrapHelper.ExecuteLine(checkCmd);
            if (check.ExitCode != 0) return -1; // -1 表示不支持

            string cmd = @"nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits";
            var result = await CliWrapHelper.ExecuteLine(cmd);
            if (result.ExitCode != 0) return -1;

            var line = result.Output?.Trim();
            if (int.TryParse(line, out var percent))
                return percent;

            return -1;
        }
        /// <summary>
        /// 图像增强
        /// </summary>
        private void ImageEnhancement()
        {
            Log.Debug("开始图像增强过程");
            try
            {
                bool[] checks =
                [
                    App.TrainModel.RandomRotationChecked,
                    App.TrainModel.RandomZoomChecked,
                    App.TrainModel.RandomBrightnessChecked,
                    App.TrainModel.RandomContrastChecked,
                    App.TrainModel.RandomHorizonFlipChecked,
                    App.TrainModel.RandomVerticalFlipChecked
                ];

                var enabledAugmentations = new List<string>();
                if (App.TrainModel.RandomRotationChecked) enabledAugmentations.Add("Rotation");
                if (App.TrainModel.RandomZoomChecked) enabledAugmentations.Add("Zoom");
                if (App.TrainModel.RandomBrightnessChecked) enabledAugmentations.Add("Brightness");
                if (App.TrainModel.RandomContrastChecked) enabledAugmentations.Add("Contrast");
                if (App.TrainModel.RandomHorizonFlipChecked) enabledAugmentations.Add("HorizontalFlip");
                if (App.TrainModel.RandomVerticalFlipChecked) enabledAugmentations.Add("VerticalFlip");

                if (checks.All(t => !t))
                {
                    Log.Information("未启用图像增强，跳过图像增强");
                    return;
                }

                Log.Information("启用图像增强: {Augmentations}", string.Join(", ", enabledAugmentations));
                PyOutput += "\n正在图像增强...";
                //先读取训练数据，然后增强图像，生成新的训练数据
                var dataSetPath = App.TrainModel.ClassifyTrainImagesPath;
                if (dataSetPath != null)
                {
                    Log.Debug("为数据集开始图像增强: {DataSetPath}", dataSetPath);
                    var dataSetClassify = Directory.GetDirectories(dataSetPath);
                    var augemnetDataFolder = Path.Combine(Environment.CurrentDirectory, "DataSet", "AugmentTrainingData");

                    Log.Debug("准备增强输出文件夹: {AugmentFolder}", augemnetDataFolder);
                    Directory.CreateDirectory(augemnetDataFolder);
                    Directory.Delete(augemnetDataFolder, true);
                    Directory.CreateDirectory(augemnetDataFolder);

                    Log.Information("处理 {ClassCount} 个图像类进行增强", dataSetClassify.Length);
                    var totalImagesProcessed = 0;

                    for (int i = 0; i < dataSetClassify.Length; i++)
                    {
                        var className = Path.GetFileName(dataSetClassify[i]);
                        var augemnetTypeFolder = Path.Combine(augemnetDataFolder, className);
                        Directory.CreateDirectory(augemnetTypeFolder);
                        var imageFiles = Directory.GetFiles(dataSetClassify[i]);

                        Log.Debug("处理类 {ClassName} ({CurrentClass}/{TotalClasses}) 包含 {ImageCount} 张图像",
                            className, i + 1, dataSetClassify.Length, imageFiles.Length);

                        foreach (var imageFile in imageFiles)
                        {
                            try
                            {
                                ImageAugmentation.AugmentImageOne(i, checks, imageFile, augemnetTypeFolder, 1);
                                totalImagesProcessed++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "图像增强失败: {ImagePath}", imageFile);
                            }
                        }
                    }
                    Log.Information("图像增强完成。处理了 {TotalImages} 张图像，跨越 {ClassCount} 个类",
                        totalImagesProcessed, dataSetClassify.Length);
                    App.TrainModel.ClassifyTrainImagesPath = augemnetDataFolder;
                    Log.Debug("更新训练数据路径为: {NewPath}", augemnetDataFolder);
                }
                else
                {
                    Log.Warning("训练数据路径为空，无法执行图像增强");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "图像增强失败");
                throw;
            }
        }
        /// <summary>
        /// 开始分类训练流程
        /// </summary>
        private async Task StartClassificationTraining()
        {
            Log.Information("开始分类训练流程");

            ScanningIndex = 0;
            IsShowNextPage = false;

            Log.Debug("初始化图表和输出信息");
            #region 初始化图标和输出信息
            await Task.Run(() =>
            {
                try
                {
                    Log.Debug("开始图像增强过程");
                    ImageEnhancement();
                    Log.Debug("图像增强成功完成");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "图像增强失败");
                    throw;
                }
            });

            PyOutput += "\n图像增强结束";
            InitialPlot();
            PyOutput = string.Empty;
            #endregion

            var currentPyLogfile = Path.Combine(App.PyTrainLogsFolderPath, "Log" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            App.TrainModel.PyTrainLogOutputPath = currentPyLogfile;
            Log.Information("分类训练日志文件创建于: {LogPath}", currentPyLogfile);

            Log.Debug("保存训练配置到 ModelParam.json");
            var jsonStr = JsonConvert.SerializeObject(App.TrainModel);
            await File.WriteAllTextAsync(Path.Combine(App.ConfigFolderPath, "ModelParam.json"), jsonStr);
            Log.Debug("训练配置保存成功");

            var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "ModelTrainer.py");
            var configPath = Path.Combine(Environment.CurrentDirectory, "Configs", "ModelParam.json");
            var arguments = $"--config {configPath}";

            Log.Information("启动Python分类训练脚本: {Script} 参数: {Arguments}", pythonScript, arguments);

            _ = Task.Run(() => ScanningThePyOutPut(cancellationTokenSource.Token));
            isPyRunning = true;

            var result = await CliWrapHelper.ExecutePythonScriptAsync(pythonScript, App.PythonVenvPath, arguments, isShowTerminal: false, null, cancellationTokenSource.Token);

            if (result.ExitCode != 0)
            {
                var errorMessage = result.Error ?? "Unknown error occurred during training";
                Log.Error("Python分类训练脚本失败，退出码 {ExitCode}: {Error}", result.ExitCode, errorMessage);
                await MessageBoxManager.GetMessageBoxStandard("训练失败", errorMessage, MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                await cancellationTokenSource.CancelAsync();
                return;
            }

            Log.Information("Python分类训练脚本成功完成");
            isPyRunning = false;

            Log.Debug("读取最终训练输出");
            await ReadPyOutputAtMeantime();

            IsShowNextPage = true;
            Log.Information("分类训练过程成功完成，显示下一页");
        }
        /// <summary>
        /// 开始检测训练流程
        /// </summary>
        private async Task StartDetectionTraining()
        {
            Log.Information("开始检测训练流程");

            ScanningIndex = 0;
            IsShowNextPage = false;

            // 验证检测任务必需的路径
            if (string.IsNullOrEmpty(App.TrainModel.TrainImagesPath) || string.IsNullOrEmpty(App.TrainModel.TrainAnnotationPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("检测训练失败", "请设置训练图像路径和标注文件路径", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                return;
            }

            InitialPlot();
            PyOutput = string.Empty;

            // 创建检测训练日志文件
            var currentPyLogfile = Path.Combine(App.PyTrainLogsFolderPath, "DetectionLog" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            App.TrainModel.PyTrainLogOutputPath = currentPyLogfile;
            Log.Information("检测训练日志文件创建于: {LogPath}", currentPyLogfile);

            // 保存检测配置
            var jsonStr = JsonConvert.SerializeObject(App.TrainModel);
            var configPath = Path.Combine(App.ConfigFolderPath, "ModelParam.json");
            await File.WriteAllTextAsync(configPath, jsonStr);
            Log.Debug("检测训练配置保存成功");

            // 启动检测训练脚本
            var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "DetectionTrainer.py");
            var arguments = $"--config {configPath}";

            Log.Information("启动Python检测训练脚本: {Script} 参数: {Arguments}", pythonScript, arguments);

            _ = Task.Run(() => ScanningTheDetectionPyOutput(cancellationTokenSource.Token));
            isPyRunning = true;

            var result = await CliWrapHelper.ExecutePythonScriptAsync(pythonScript, App.PythonVenvPath, arguments, isShowTerminal: false, null, cancellationTokenSource.Token);

            if (result.ExitCode != 0)
            {
                var errorMessage = result.Error ?? "Unknown error occurred during detection training";
                Log.Error("Python检测训练脚本失败，退出码 {ExitCode}: {Error}", result.ExitCode, errorMessage);
                await MessageBoxManager.GetMessageBoxStandard("检测训练失败", errorMessage, MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                await cancellationTokenSource.CancelAsync();
                return;
            }

            Log.Information("Python检测训练脚本成功完成");
            isPyRunning = false;

            await ReadDetectionPyOutputAtMeantime();
            IsShowNextPage = true;
            Log.Information("检测训练过程成功完成，显示下一页");
        }
        /// <summary>
        /// 初始化图表线条和进度
        /// </summary>
        private void InitialPlot()
        {
            Log.Debug("初始化图表 - Initializing training chart series");
            EpochState.CurrentEpoch = 0;
            EpochState.TotalEpochs = App.TrainModel?.Epochs;
            try
            {
                TrainAccValues = [];
                TrainLossValues = [];
                ValidationAccValues = [];
                ValidationLossValues = [];
                Series =
            [new LineSeries<ObservableValue>(TrainAccValues)
                {
                    Name = "Train Acc Values",
                    Fill = null,
                    GeometrySize = 5,
                    GeometryStroke = new SolidColorPaint(SKColors.Orange, 2),
                    Stroke = new SolidColorPaint()
                    {
                        Color = SKColors.Orange,
                        StrokeThickness = 2
                    }
                },
            new LineSeries<ObservableValue>(ValidationLossValues)
                {
                    Name = "Validation Loss Values",
                    Fill = null,
                    GeometrySize = 5,
                    GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Stroke = new SolidColorPaint()
                    {
                        Color = SKColors.DodgerBlue,
                        StrokeThickness = 2
                    }
                },
            new LineSeries<ObservableValue>(ValidationAccValues)
                {
                    Name = "Validation Acc Values",
                    Fill = null,
                    GeometrySize = 5,
                    GeometryStroke = new SolidColorPaint(SKColors.Blue, 2),
                    Stroke = new SolidColorPaint()
                    {
                        Color = SKColors.Blue,
                        StrokeThickness = 2
                    }
                },
            new LineSeries<ObservableValue>(TrainLossValues)
                {
                    Name = "Train Loss Values",
                    Fill = null,
                    GeometrySize = 5,
                    GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    Stroke = new SolidColorPaint()
                    {
                        Color = SKColors.OrangeRed,
                        StrokeThickness = 2
                    }
                }];
                Log.Debug("Training chart series initialized successfully with {SeriesCount} series", Series?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize training chart series");
                throw;
            }
        }

        /// <summary>
        /// Py文件执行时，处理各种任务
        /// </summary>
        /// <returns></returns>
        private async Task ScanningThePyOutPut(CancellationToken token)
        {
            Log.Information("Starting Python training output monitoring");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var logPath = App.TrainModel?.PyTrainLogOutputPath;
                        if (string.IsNullOrEmpty(logPath))
                        {
                            Log.Warning("Training log path is null or empty, waiting...");
                            await Task.Delay(3000, CancellationToken.None);
                            continue;
                        }

                        if (!File.Exists(logPath))
                        {
                            Log.Debug("Training log file not found yet: {LogPath}", logPath);
                            await Task.Delay(3000, CancellationToken.None);
                            continue;
                        }

                        Log.Debug("Training log file found, reading output");
                        await ReadPyOutputAtMeantime();
                        await Task.Delay(3000, CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Information("Python training output monitoring cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error occurred while monitoring Python training process");
                        await Task.Delay(3000, CancellationToken.None); // Wait before retrying
                    }
                }
            }
            finally
            {
                Log.Information("Python training output monitoring stopped");
            }
        }

        /// <summary>
        /// 读取Py脚本输出的json，绘图，输出和控制进度条
        /// </summary>
        private async Task ReadPyOutputAtMeantime()
        {
            var reTryCounter = 0;
            var logFilePath = App.TrainModel?.PyTrainLogOutputPath;

            if (string.IsNullOrEmpty(logFilePath))
            {
                Log.Error("Cannot read Python output: PyTrainLogOutputPath is null or empty");
                return;
            }

            Log.Debug("Waiting for training log file to be created: {LogPath}", logFilePath);

            while (true)
            {
                if (reTryCounter > 3)
                {
                    Log.Error("Training log file could not be created after {RetryCount} attempts: {LogPath}", reTryCounter, logFilePath);
                    await MessageBoxManager.GetMessageBoxStandard("训练失败", $"ModelTrainer.py无法创建训练日志\n{logFilePath}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync();
                    return;
                }

                if (!File.Exists(logFilePath))
                {
                    await Task.Delay(1000);
                    reTryCounter++;
                    Log.Debug("Waiting for training log file, attempt {Attempt}", reTryCounter);
                }
                else
                {
                    Log.Information("Training log file found after {Attempt} attempts", reTryCounter);
                    break;
                }
            }
            try
            {
                string jsonStr;
                // 使用FileStream来减少文件锁定时间
                using (var fileStream = new FileStream(App.TrainModel.PyTrainLogOutputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    jsonStr = await streamReader.ReadToEndAsync();
                }
                Log.Debug("Read training log content, length: {Length} characters", jsonStr.Length);

                var pyExecuteOutput = JsonConvert.DeserializeObject<TrainingLog>(jsonStr);

                if (pyExecuteOutput is { Entries.Count: > 0 })
                {
                    if (ScanningIndex >= pyExecuteOutput.Entries.Count)
                    {
                        Log.Debug("No new entries to process. ScanningIndex: {Index}, TotalEntries: {Count}", ScanningIndex, pyExecuteOutput.Entries.Count);
                        return;
                    }

                    Log.Debug("Processing {NewEntries} new training log entries", pyExecuteOutput.Entries.Count - ScanningIndex);

                    for (var i = ScanningIndex; i < pyExecuteOutput.Entries.Count; i++)
                    {
                        var entry = pyExecuteOutput.Entries[i];
                        Log.Debug("Processing entry {EntryIndex}: Type={Type}, Epoch={Epoch}, Message={Message}",
                            i, entry.Type, entry.Epoch, entry.Message?.Substring(0, Math.Min(50, entry.Message?.Length ?? 0)));

                        //画图方面，需要找到type为Validation的消息
                        if (string.Equals(entry.Type, "Validation", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TrainAccValues != null && TrainLossValues != null && ValidationAccValues != null && ValidationLossValues != null)
                            {
                                var trainAcc = entry.Metrics?.TrainAccuracy;
                                var trainLoss = entry.Metrics?.TrainLoss;
                                var valAcc = entry.Metrics?.ValidationAccuracy;
                                var valLoss = entry.Metrics?.ValidationLoss;

                                TrainAccValues.Add(new ObservableValue() { Value = trainAcc });
                                TrainLossValues.Add(new ObservableValue() { Value = trainLoss });
                                ValidationAccValues.Add(new ObservableValue() { Value = valAcc });
                                ValidationLossValues.Add(new ObservableValue() { Value = valLoss });
                                EpochState.CurrentEpoch = entry.Epoch;

                                Log.Information("Epoch {Epoch} metrics - Train Acc: {TrainAcc:F4}, Train Loss: {TrainLoss:F4}, Val Acc: {ValAcc:F4}, Val Loss: {ValLoss:F4}",
                                    entry.Epoch, trainAcc, trainLoss, valAcc, valLoss);
                            }
                            else
                            {
                                Log.Warning("Chart data series are null, cannot update validation metrics for epoch {Epoch}", entry.Epoch);
                            }
                        }

                        //打印输出信息
                        if (!string.IsNullOrEmpty(entry.Message))
                        {
                            PyOutput += entry.Message + "\r\n";
                        }
                        ScanningIndex++;
                    }

                    Log.Debug("Updated ScanningIndex to {Index}, processing complete", ScanningIndex);
                }
                else
                {
                    Log.Warning("Training log is empty or malformed: {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                }

                if (!isPyRunning && EpochState.CurrentEpoch < EpochState.TotalEpochs)
                {
                    var oldTotal = EpochState.TotalEpochs;
                    EpochState.TotalEpochs = EpochState.CurrentEpoch;
                    Log.Information("Training completed early. Updated total epochs from {OldTotal} to {NewTotal}", oldTotal, EpochState.TotalEpochs);
                }
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to parse training log JSON from {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to read training log file at {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error while processing training log from {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
        }

        /// <summary>
        /// Py文件执行时，处理检测训练的各种任务
        /// </summary>
        /// <returns></returns>
        private async Task ScanningTheDetectionPyOutput(CancellationToken token)
        {
            Log.Information("Starting Python detection training output monitoring");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var logPath = App.TrainModel?.PyTrainLogOutputPath;
                        if (string.IsNullOrEmpty(logPath))
                        {
                            Log.Warning("Detection training log path is null or empty, waiting...");
                            await Task.Delay(3000, CancellationToken.None);
                            continue;
                        }

                        if (!File.Exists(logPath))
                        {
                            Log.Debug("Detection training log file not found yet: {LogPath}", logPath);
                            await Task.Delay(3000, CancellationToken.None);
                            continue;
                        }

                        Log.Debug("Detection training log file found, reading output");
                        await ReadDetectionPyOutputAtMeantime();
                        await Task.Delay(3000, CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Information("Python detection training output monitoring cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error occurred while monitoring Python detection training process");
                        await Task.Delay(3000, CancellationToken.None);
                    }
                }
            }
            finally
            {
                Log.Information("Python detection training output monitoring stopped");
            }
        }

        /// <summary>
        /// 读取检测训练Py脚本输出的json，绘图，输出和控制进度条
        /// </summary>
        private async Task ReadDetectionPyOutputAtMeantime()
        {
            var logFilePath = App.TrainModel?.PyTrainLogOutputPath;

            if (string.IsNullOrEmpty(logFilePath))
            {
                Log.Error("Cannot read Python detection output: PyTrainLogOutputPath is null or empty");
                return;
            }

            Log.Debug("Waiting for detection training log file to be created: {LogPath}", logFilePath);

            var reTryCounter = 0;
            while (true)
            {
                if (reTryCounter > 3)
                {
                    Log.Error("Detection training log file could not be created after {RetryCount} attempts: {LogPath}", reTryCounter, logFilePath);
                    await MessageBoxManager.GetMessageBoxStandard("检测训练失败", $"DetectionTrainer.py无法创建训练日志\n{logFilePath}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync();
                    return;
                }

                if (!File.Exists(logFilePath))
                {
                    await Task.Delay(1000);
                    reTryCounter++;
                    Log.Debug("Waiting for detection training log file, attempt {Attempt}", reTryCounter);
                }
                else
                {
                    Log.Information("Detection training log file found after {Attempt} attempts", reTryCounter);
                    break;
                }
            }

            try
            {
                string jsonStr;
                using (var fileStream = new FileStream(App.TrainModel.PyTrainLogOutputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    jsonStr = await streamReader.ReadToEndAsync();
                }
                Log.Debug("Read detection training log content, length: {Length} characters", jsonStr.Length);

                var pyExecuteOutput = JsonConvert.DeserializeObject<TrainingLog>(jsonStr);

                if (pyExecuteOutput is { Entries.Count: > 0 })
                {
                    if (ScanningIndex >= pyExecuteOutput.Entries.Count)
                    {
                        Log.Debug("No new entries to process. ScanningIndex: {Index}, TotalEntries: {Count}", ScanningIndex, pyExecuteOutput.Entries.Count);
                        return;
                    }

                    Log.Debug("Processing {NewEntries} new detection training log entries", pyExecuteOutput.Entries.Count - ScanningIndex);

                    for (var i = ScanningIndex; i < pyExecuteOutput.Entries.Count; i++)
                    {
                        var entry = pyExecuteOutput.Entries[i];
                        Log.Debug("Processing entry {EntryIndex}: Type={Type}, Epoch={Epoch}, Message={Message}",
                            i, entry.Type, entry.Epoch, entry.Message?.Substring(0, Math.Min(50, entry.Message?.Length ?? 0)));

                        // 处理检测特有的验证消息
                        if (string.Equals(entry.Type, "DetectionValidation", StringComparison.OrdinalIgnoreCase))
                        {
                            if (ValidationLossValues != null)
                            {
                                var trainLoss = entry.Metrics?.TrainLoss;
                                var valLoss = entry.Metrics?.ValidationLoss;

                                // 检测任务主要关注损失值
                                TrainLossValues.Add(new ObservableValue() { Value = trainLoss });
                                ValidationLossValues.Add(new ObservableValue() { Value = valLoss });
                                EpochState.CurrentEpoch = entry.Epoch;

                                Log.Information("Detection Epoch {Epoch} metrics - Train Loss: {TrainLoss:F4}, Val Loss: {ValLoss:F4}",
                                    entry.Epoch, trainLoss, valLoss);
                            }
                            else
                            {
                                Log.Warning("Chart data series are null, cannot update detection validation metrics for epoch {Epoch}", entry.Epoch);
                            }
                        }

                        // 打印输出信息
                        if (!string.IsNullOrEmpty(entry.Message))
                        {
                            PyOutput += entry.Message + "\r\n";
                        }
                        ScanningIndex++;
                    }

                    Log.Debug("Updated ScanningIndex to {Index}, processing complete", ScanningIndex);
                }
                else
                {
                    Log.Warning("Detection training log is empty or malformed: {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                }

                if (!isPyRunning && EpochState.CurrentEpoch < EpochState.TotalEpochs)
                {
                    var oldTotal = EpochState.TotalEpochs;
                    EpochState.TotalEpochs = EpochState.CurrentEpoch;
                    Log.Information("Detection training completed early. Updated total epochs from {OldTotal} to {NewTotal}", oldTotal, EpochState.TotalEpochs);
                }
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to parse detection training log JSON from {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to read detection training log file at {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error while processing detection training log from {LogPath}", App.TrainModel.PyTrainLogOutputPath);
                throw;
            }
        }
        #endregion
    }
}
