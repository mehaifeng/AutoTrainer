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
        private CancellationTokenSource cancellationTokenSource;
        private int ScanningIndex = 0;
        public TrainingViewModel()
        {
            Log.Information("TrainingViewModel 初始化完成");
            try
            {
                InitialPlot();
                cancellationTokenSource = new();
                Log.Debug("TrainingViewModel 初始化成功完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TrainingViewModel 初始化失败");
                throw;
            }
        }

        #region 可绑定属性

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
        [ObservableProperty]
        private bool isShowNextPage = false;
        [ObservableProperty]
        private string? modelParamStr;
        [ObservableProperty]
        private string? pyOutput;
        [ObservableProperty]
        private EpochState epochState = new();

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

                Log.Debug("为模型加载参数: {Model}", modelParam.PretrainedModel);

                sb.AppendLine("模型: " + modelParam.PretrainedModel);
                sb.AppendLine("学习率: " + modelParam.LearningRate);
                sb.AppendLine("优化器: " + modelParam.Optimizer);
                sb.AppendLine("学习率调度器: " + modelParam.LrScheduler);
                sb.AppendLine("权重衰减: " + modelParam.WeightDecay);
                sb.AppendLine("批量大小: " + modelParam.BatchSize);
                sb.AppendLine("训练轮数: " + modelParam.Epochs);
                sb.AppendLine("早停轮数: " + modelParam.EarlyStoppingRounds);
                sb.AppendLine("验证集比例: " + modelParam.ValidationSplit);
                sb.AppendLine("训练数据路径: " + modelParam.TrainDataPath);
                sb.AppendLine("是否使用随机水平翻转: " + modelParam.RandomHorizonFlipChecked);
                sb.AppendLine("是否使用随机垂直翻转: " + modelParam.RandomVerticalFlipChecked);
                sb.AppendLine("是否使用随机旋转: " + modelParam.RandomRotationChecked);
                sb.AppendLine("是否使用随机亮度: " + modelParam.RandomBrightnessChecked);
                sb.AppendLine("是否使用随机对比度: " + modelParam.RandomContrastChecked);
                sb.AppendLine("验证数据路径: " + modelParam.ValDataPath);
                sb.AppendLine("模型保存路径: " + modelParam.ModelOutputPath);
                sb.AppendLine("训练日志输出路径: " + modelParam.PyTrainLogOutputPath);
                EpochState.TotalEpochs = modelParam.Epochs;
                ModelParamStr = sb.ToString();

                Log.Information("训练参数加载成功。轮数: {Epochs}, 批量大小: {BatchSize}, 模型: {Model}",
                    modelParam.Epochs, modelParam.BatchSize, modelParam.PretrainedModel);
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

                ScanningIndex = 0;
                IsShowNextPage = false;
                cancellationTokenSource = new();

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
                Log.Information("训练日志文件创建于: {LogPath}", currentPyLogfile);

                Log.Debug("保存训练配置到 ModelParam.json");
                var jsonStr = JsonConvert.SerializeObject(App.TrainModel);
                await File.WriteAllTextAsync(Path.Combine(App.ConfigFolderPath, "ModelParam.json"), jsonStr);
                Log.Debug("训练配置保存成功");

                var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "ModelTrainer.py");
                var configPath = Path.Combine(Environment.CurrentDirectory, "Configs", "ModelParam.json");
                var arguments = $"--config {configPath}";

                Log.Information("启动Python训练脚本: {Script} 参数: {Arguments}", pythonScript, arguments);

                _ = Task.Run(() => ScanningThePyOutPut(cancellationTokenSource.Token));
                isPyRunning = true;

                var result = await CliWrapHelper.ExecutePythonScriptAsync(pythonScript, App.PythonVenvPath, arguments, isShowTerminal: false, null, cancellationTokenSource.Token);

                if (result.ExitCode != 0)
                {
                    var errorMessage = result.Error ?? "Unknown error occurred during training";
                    Log.Error("Python训练脚本失败，退出码 {ExitCode}: {Error}", result.ExitCode, errorMessage);
                    await MessageBoxManager.GetMessageBoxStandard("训练失败", errorMessage, MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
                    await cancellationTokenSource.CancelAsync();
                    return;
                }

                Log.Information("Python训练脚本成功完成");
                isPyRunning = false;

                Log.Debug("读取最终训练输出");
                await ReadPyOutputAtMeantime();

                IsShowNextPage = true;
                Log.Information("训练过程成功完成，显示下一页");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动或完成训练过程失败");
                isPyRunning = false;
                await MessageBoxManager.GetMessageBoxStandard("训练失败", $"训练过程中发生错误: {ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowAsync();
            }
        }
        [RelayCommand]
        private async Task GotoNextPage(UserControl o)
        {
            Log.Debug("训练完成后导航到下一页");
            try
            {
                if (o.Parent != null && o.Parent.Parent is TabControl control)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => control.SelectedIndex = 4);
                    Log.Information("成功导航到验证页面（标签索引 4）");
                }
                else
                {
                    Log.Warning("找不到用于导航的TabControl，父级结构: {Parent}", o.Parent?.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导航到下一页失败");
                throw;
            }
        }
        #endregion

        #region 函数
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
                var dataSetPath = App.TrainModel.TrainDataPath;
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
                    App.TrainModel.TrainDataPath = augemnetDataFolder;
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
        /// 初始化图表线条
        /// </summary>
        private void InitialPlot()
        {
            Log.Debug("Initializing training chart series");
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
        #endregion
    }
}
