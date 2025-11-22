using AutoTrainer.Helpers;
using AutoTrainer.Models;
using AutoTrainer.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
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
    #region UI绑定数据模型
    public partial class ClassificationMetricsModel : ObservableObject
    {
        [ObservableProperty] private string accuracy = "0%";
        [ObservableProperty] private string recall = "0%";
        [ObservableProperty] private string f1Score = "0.0";
        [ObservableProperty] private string time = "0.0s";
    }

    public partial class DetectionMetricsModel : ObservableObject
    {
        [ObservableProperty] private string map50 = "0%";
        [ObservableProperty] private string map50_95 = "0%";
        [ObservableProperty] private string precision = "0%";
        [ObservableProperty] private string recall = "0%";
        [ObservableProperty] private string time = "0.0s";
    }

    public partial class ClassifiedImageGroup : ObservableObject
    {
        [ObservableProperty] private string className;
        [ObservableProperty] private ObservableCollection<Bitmap> images = new();
    }

    public partial class DetectedImageResult : ObservableObject
    {
        [ObservableProperty] private Bitmap sourceImage;
        [ObservableProperty] private string imagePath;
        [ObservableProperty] private int objectCount;
    }

    public partial class BoundingBox : ObservableObject
    {
        [ObservableProperty] private Rect rect;
        [ObservableProperty] private IBrush stroke;
        [ObservableProperty] private double strokeThickness;
        [ObservableProperty] private bool isPrediction;
        [ObservableProperty] private string label;
    }
    #endregion

    public partial class ValidModelPerformanceViewModel : ViewModelBase
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        #region 可绑定属性
        [ObservableProperty]
        private bool isCheckedClassifyMode = true;
        [ObservableProperty]
        private string validDatasetPath = string.Empty;
        [ObservableProperty]
        private ObservableCollection<Bitmap> validDatasetImagePreviews;
        [ObservableProperty]
        private string modelWeightsPath;
        [ObservableProperty]
        private string cocoAnnotationPath;
        [ObservableProperty]
        private double confidenceThreshold = 0.5;
        [ObservableProperty]
        private bool isVerifying;
        [ObservableProperty]
        private int resultTabIndex;
        [ObservableProperty]
        private string verificationOutput;

        // 指标
        [ObservableProperty]
        private ClassificationMetricsModel classificationMetrics = new();
        [ObservableProperty]
        private DetectionMetricsModel detectionMetrics = new();

        // 图表
        [ObservableProperty]
        private ISeries[] chartSeries;
        [ObservableProperty]
        private ICartesianAxis[] chartXAxes;
        [ObservableProperty]
        private ICartesianAxis[] chartYAxes;

        // 可视化结果
        [ObservableProperty]
        private ObservableCollection<ClassifiedImageGroup> classifiedResults = new();
        [ObservableProperty]
        private ObservableCollection<DetectedImageResult> detectionResults = new();
        #endregion

        public ValidModelPerformanceViewModel()
        {
            ValidDatasetImagePreviews = [];
            ChartSeries = [];
            ChartXAxes = [];
            ChartYAxes = [];
            ClearResults();
        }

        #region 命令
        [RelayCommand]
        private void OpenImageLocation(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var argument = $"/select, \"{imagePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "无法打开文件位置");
                }
            }
        }

        [RelayCommand]
        private void Loaded()
        {
            if(App.TrainModel?.TaskType == "classification")
            {
                IsCheckedClassifyMode = true;
            }
            else if(App.TrainModel?.TaskType == "detection")
            {
                IsCheckedClassifyMode = false;
            }
        }
        /// <summary>
        /// 选择模型权重文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SelectWeightsFile()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime destop ? destop.MainWindow as Control : null;
            if (mainWindow != null)
            {
                var toplevel = TopLevel.GetTopLevel(mainWindow);
                if (toplevel != null)
                {
                    var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择模型权重文件",
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("PyTorch Models") { Patterns = new[] { "*.pth" } } }
                    });
                    if (files.Count > 0)
                    {
                        ModelWeightsPath = files[0].TryGetLocalPath() ?? string.Empty;
                    }
                }
            }
        }
        /// <summary>
        /// 选择COCO标注文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SelectCocoFile()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime destop ? destop.MainWindow as Control : null;
            if (mainWindow != null)
            {
                var toplevel = TopLevel.GetTopLevel(mainWindow);
                if (toplevel != null)
                {
                    var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择COCO标注文件",
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } }
                    });
                    if (files.Count > 0)
                    {
                        CocoAnnotationPath = files[0].TryGetLocalPath() ?? string.Empty;
                    }
                }
            }
        }
        /// <summary>
        /// 开始执行验证
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task StartVerification()
        {
            Log.Information("开始验证过程");

            #region 输入验证
            if (string.IsNullOrEmpty(ModelWeightsPath) || !File.Exists(ModelWeightsPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请提供一个有效的模型权重文件路径").ShowWindowAsync();
                return;
            }
            if (string.IsNullOrEmpty(ValidDatasetPath) || !Directory.Exists(ValidDatasetPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请提供一个有效的验证集目录路径").ShowWindowAsync();
                return;
            }
            if (!IsCheckedClassifyMode && (string.IsNullOrEmpty(CocoAnnotationPath) || !File.Exists(CocoAnnotationPath)))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "目标检测任务需要一个COCO标注文件").ShowWindowAsync();
            }
            if(string.IsNullOrEmpty(App.PythonVenvPath) || !Directory.Exists(Path.Combine(App.PythonVenvPath, "Scripts")))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "请先在模型仓库页配置一个有效的Python虚拟环境").ShowWindowAsync();
                return;
            }
            #endregion

            IsVerifying = true;
            ResultTabIndex = 1;
            VerificationOutput = string.Empty;
            ClassifiedResults.Clear();
            DetectionResults.Clear();

            try
            {
                // 创建配置文件
                var config = new
                {
                    task_type = IsCheckedClassifyMode ? "classification" : "detection",
                    model_weights_path = ModelWeightsPath,
                    validation_data_path = ValidDatasetPath,
                    coco_annotation_path = CocoAnnotationPath,
                    confidence_threshold = ConfidenceThreshold
                };
                var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                var configPath = Path.Combine(App.ConfigFolderPath, "validation_config.json");
                await File.WriteAllTextAsync(configPath, configJson);

                // 选择脚本
                var scriptName = IsCheckedClassifyMode ? "ClassificationValidator.py" : "DetectionValidator.py";
                var pythonScript = Path.Combine(Environment.CurrentDirectory, "PyScripts", "Inference", scriptName);

                var result = await CliWrapHelper.ExecutePythonScriptAsync(
                    pythonScript,
                    App.PythonVenvPath,
                    $"--config \"{configPath}\"",
                    false,
                    onOutputReceived: (output) =>
                    {
                        Dispatcher.UIThread.InvokeAsync(() => VerificationOutput += output + "\r\n");
                    },
                    _cancellationTokenSource.Token);

                if (result.ExitCode != 0)
                {
                    await MessageBoxManager.GetMessageBoxStandard("验证失败", $"Python脚本执行出错: \n{result.Error}").ShowWindowAsync();
                    return;
                }

                // 处理结果
                var resultFileName = IsCheckedClassifyMode ? "classification_results.json" : "detection_results.json";
                var resultPath = Path.Combine(App.ConfigFolderPath, resultFileName);
                if (File.Exists(resultPath))
                {
                    var resultJson = await File.ReadAllTextAsync(resultPath);
                    var validationResult = JsonConvert.DeserializeObject<ValidationResult>(resultJson);
                    if (validationResult != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (IsCheckedClassifyMode)
                                UpdateClassificationResults(validationResult);
                            else
                                UpdateDetectionResults(validationResult);
                        });
                        ResultTabIndex = 0; // 切换到结果页
                    }
                }
                else
                {
                    await MessageBoxManager.GetMessageBoxStandard("错误", "未找到验证结果文件").ShowWindowAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "验证过程中发生意外错误");
                await MessageBoxManager.GetMessageBoxStandard("严重错误", ex.Message).ShowWindowAsync();
            }
            finally
            {
                IsVerifying = false;
            }
        }
        /// <summary>
        /// 选择验证集目录
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SelectValidDatasetPath()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime destop ? destop.MainWindow as Control : null;
            if (mainWindow != null)
            {
                var toplevel = TopLevel.GetTopLevel(mainWindow);
                if (toplevel != null)
                {
                    var folder = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                    {
                        Title = "选择验证集目录",
                        AllowMultiple = false,
                    });
                    if (folder.Count > 0)
                    {
                        ValidDatasetPath = folder[0].TryGetLocalPath() ?? string.Empty;
                        await LoadValidDatasetImagePreview();
                    }
                }
            }
        }
        #endregion

        #region 函数

        private void UpdateClassificationResults(ValidationResult result)
        {
            // 更新指标
            ClassificationMetrics.Accuracy = $"{result.Metrics.Accuracy:P2}";
            ClassificationMetrics.Recall = $"{result.Metrics.Recall:P2}";
            ClassificationMetrics.F1Score = $"{result.Metrics.F1Score:F2}";
            ClassificationMetrics.Time = $"{result.Metrics.Time:F1}s";

            // 更新图表 (混淆矩阵)
            var labels = result.ChartData.Labels;
            var matrixData = result.ChartData.ConfusionMatrix;
            if (matrixData != null && labels != null)
            {
                ChartSeries = new ISeries[]
                {
                    new HeatSeries<WeightedPoint>
                    {
                        Values = matrixData.SelectMany((row, i) => row.Select((value, j) => new WeightedPoint(j, i, value))).ToList(),
                        HeatMap = new[]
                        {
                            new SKColor(255, 247, 243).AsLvcColor(), // Lightest
                            new SKColor(253, 224, 221).AsLvcColor(),
                            new SKColor(252, 197, 192).AsLvcColor(),
                            new SKColor(250, 159, 154).AsLvcColor(),
                            new SKColor(247, 120, 115).AsLvcColor(),
                            new SKColor(244, 67, 54).AsLvcColor()      // Darkest
                        }
                    }
                };
                ChartXAxes = new ICartesianAxis[]
                {
                    new Axis { Name = "Predicted", Labels = labels, LabelsRotation = -45 }
                };
                ChartYAxes = new ICartesianAxis[]
                {
                    new Axis { Name = "Actual", Labels = labels }
                };
            }
            
            // 更新可视化结果
            var grouped = result.ImageResults.GroupBy(ir => ir.PredictedClass);
            foreach (var group in grouped)
            {
                var imageGroup = new ClassifiedImageGroup { ClassName = group.Key };
                foreach (var item in group)
                {
                    if (File.Exists(item.Path))
                        imageGroup.Images.Add(new Bitmap(item.Path));
                }
                ClassifiedResults.Add(imageGroup);
            }
        }

        private void UpdateDetectionResults(ValidationResult result)
        {
            // 更新指标
            DetectionMetrics.Map50 = $"{result.Metrics.Map50:P2}";
            DetectionMetrics.Map50_95 = $"{result.Metrics.Map50_95:P2}";
            DetectionMetrics.Precision = $"{result.Metrics.Precision:P2}";
            DetectionMetrics.Recall = $"{result.Metrics.Recall:P2}";
            DetectionMetrics.Time = $"{result.Metrics.Time:F1}s";

            // 更新图表 (PR 曲线)
            if (result.ChartData?.PrCurvePoints != null)
            {
                ChartSeries = new ISeries[]
                {
                    new LineSeries<PointData>
                    {
                        Values = result.ChartData.PrCurvePoints,
                        Mapping = (point, index) => new(point.X, point.Y),
                        GeometrySize = 0,
                        Fill = null
                    }
                };
                ChartXAxes = new ICartesianAxis[]
                {
                    new Axis { Name = "Recall" }
                };
                ChartYAxes = new ICartesianAxis[]
                {
                    new Axis { Name = "Precision" }
                };
            }
            
            // 更新可视化结果
            foreach (var item in result.ImageResults)
            {
                if (!File.Exists(item.Path)) continue;

                var totalBoxes = 0;
                if (item.GroundTruthBoxes != null) totalBoxes += item.GroundTruthBoxes.Count;
                if (item.PredictedBoxes != null) totalBoxes += item.PredictedBoxes.Count;

                var detectedResult = new DetectedImageResult
                {
                    SourceImage = new Bitmap(item.Path),  // Path now points to annotated image
                    ImagePath = item.Path,
                    ObjectCount = totalBoxes
                };

                DetectionResults.Add(detectedResult);
            }
        }


        /// <summary>
        /// 加载验证集预览图片
        /// </summary>
        /// <returns></returns>
        private async Task LoadValidDatasetImagePreview()
        {
            ValidDatasetImagePreviews.Clear();
            if (!string.IsNullOrEmpty(ValidDatasetPath) && Directory.Exists(ValidDatasetPath))
            {
                var allowExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".png", ".jpeg", ".bmp", ".tiff", ".webp" };
                
                // 仅加载顶层目录的图片
                var files = Directory.EnumerateFiles(ValidDatasetPath, "*", SearchOption.TopDirectoryOnly)
                                     .Where(file => allowExtensions.Contains(Path.GetExtension(file)))
                                     .Take(50); // 最多预览50张

                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            using var originalBitmap = new Bitmap(file);
                            // 统一缩放到90x90的预览图
                            var finalBitmap = originalBitmap.CreateScaledBitmap(new PixelSize(90, 90), BitmapInterpolationMode.HighQuality);
                            Dispatcher.UIThread.InvokeAsync(() => ValidDatasetImagePreviews.Add(finalBitmap));
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "加载预览图片失败: {File}", file);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 从训练结果设置验证初始状态
        /// </summary>
        /// <param name="trainModel"></param>
        public async Task SetInitialStateFromTraining(TrainModel trainModel)
        {
            Log.Information("从训练结果设置验证初始状态");
            if (trainModel == null)
            {
                Log.Warning("传递给SetInitialStateFromTraining的TrainModel为null");
                return;
            }

            IsCheckedClassifyMode = (trainModel.TaskType == "classification");

            // Assuming ModelOutputPath in TrainModel is the directory where the model is saved.
            // We need to find the actual .pt file.
            if (!string.IsNullOrEmpty(trainModel.ModelOutputPath) && Directory.Exists(trainModel.ModelOutputPath))
            {
                // Look for the latest .pt file in the output directory and its subdirectories
                var ptFiles = Directory.EnumerateFiles(trainModel.ModelOutputPath, "*.pt", SearchOption.AllDirectories)
                                       .OrderByDescending(f => new System.IO.FileInfo(f).CreationTime) // Get the latest one
                                       .FirstOrDefault();
                if (ptFiles != null)
                {
                    ModelWeightsPath = ptFiles;
                    Log.Debug("从训练结果中设置模型权重路径: {ModelWeightsPath}", ModelWeightsPath);
                }
                else
                {
                    Log.Warning("在训练输出路径中未找到.pt模型文件: {ModelOutputPath}", trainModel.ModelOutputPath);
                    ModelWeightsPath = string.Empty;
                }
            }
            else
            {
                Log.Warning("TrainModel.ModelOutputPath无效或不存在: {ModelOutputPath}", trainModel.ModelOutputPath);
                ModelWeightsPath = string.Empty;
            }

            // Clear any previous validation data
            ClearResults();
            Log.Information("验证初始状态设置完成");
        }

        /// <summary>
        /// 清除所有验证结果和UI状态
        /// </summary>
        private void ClearResults()
        {
            // Clear metrics
            ClassificationMetrics.Accuracy = "0%";
            ClassificationMetrics.Recall = "0%";
            ClassificationMetrics.F1Score = "0.0";
            ClassificationMetrics.Time = "0.0s";

            DetectionMetrics.Map50 = "0%";
            DetectionMetrics.Map50_95 = "0%";
            DetectionMetrics.Precision = "0%";
            DetectionMetrics.Recall = "0%";
            DetectionMetrics.Time = "0.0s";

            // Clear charts
            ChartSeries = Array.Empty<ISeries>();
            ChartXAxes = Array.Empty<ICartesianAxis>();
            ChartYAxes = Array.Empty<ICartesianAxis>();

            // Clear visual results
            ClassifiedResults.Clear();
            DetectionResults.Clear();

            // Reset output log
            VerificationOutput = string.Empty;
        }
        #endregion

    }
}