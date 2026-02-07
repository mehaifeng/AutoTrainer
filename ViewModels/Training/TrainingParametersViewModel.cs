using AutoTrainer.Models;
using AutoTrainer.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class TrainingParametersViewModel : ViewModelBase
    {
        public TrainingParametersViewModel()
        {
            LearningRates = [0.1f, 0.01f, 0.001f, 0.0003f, 0.0001f, 0.00001f];
            BatchSizes = [1, 2, 4, 8, 16, 32, 64];
            Optimizers = ["AdamW", "Adam", "SGD"];
            ValidationSetRates = [0.1f, 0.2f, 0.3f];
            SchedulingStrategies = ["ReduceLROnPlateau", "StepLR"];
            LossFunctionTypes = ["CrossEntropyLoss", "BCEWithLogitsLoss", "KLDivLoss"];
            Reductions = ["mean", "sum"];
            KLDivLoss_Reductions = ["batchmean", "mean", "sum"];
            SelectedLearningRate = LearningRates[1];
            SelectedBatchSize = BatchSizes[1];
            SelectedValidationSetRate = ValidationSetRates[1];
            SelectedOptimizer = Optimizers[0];
            SelectedStrategy = SchedulingStrategies[0];
            SelectedLossFunction = LossFunctionTypes[0];
            Epochs = 25;
            EarlyStopRound = 5;
            EarlyStopDelta = 0.0001f;
            // 初始化自定义模型名称
            CustomModelName = string.IsNullOrWhiteSpace(App.TrainModel?.CustomModelName)
                ? App.TrainModel?.PretrainedModel
                : App.TrainModel?.CustomModelName;
            this.PropertyChanged += TrainingParametersViewModel_PropertyChanged;
        }

        private void TrainingParametersViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsVisibleNextStep")
            {
                IsVisibleNextStep = false;
            }

            // 检查任务类型变化
            if (e.PropertyName == nameof(App.TrainModel.TaskType))
            {
                UpdateUIForTaskType();
                return;
            }
        }
        
        #region 可绑定属性
        /// <summary>
        /// 自定义模型名称
        /// </summary>
        [ObservableProperty]
        private string? customModelName;
        partial void OnCustomModelNameChanged(string? value)
        {
            if (App.TrainModel != null)
            {
                App.TrainModel.CustomModelName = value;
            }
        }
        /// <summary>
        /// 分类任务训练集地址
        /// </summary>
        [ObservableProperty]
        private string? classifyTrainSetPath;
        
        partial void OnClassifyTrainSetPathChanged(string? value)
        {
            // 当训练集路径改变时，如果是分类任务且有值，自动检测ImageFolder格式
            // 自动触发时不显示警告（showWarning=false）
            if (!IsDetectionTask && !string.IsNullOrEmpty(value))
            {
                _ = CheckAndHandleImageFolderFormat(value, showWarning: false);
            }
        }

        /// <summary>
        /// 分类任务验证集地址
        /// </summary>
        [ObservableProperty]
        private string? classifyValidationSetPath;

        partial void OnClassifyValidationSetPathChanged(string? value)
        {
            UpdateValidationSplitEnabled();
        }

        /// <summary>
        /// 分类任务标注文件地址
        /// </summary>
        [ObservableProperty]
        private string? classifyAnnotationPath;

        /// <summary>
        /// 训练集是否为ImageFolder格式
        /// </summary>
        [ObservableProperty]
        private bool isImageFolderFormat = false;

        /// <summary>
        /// 从ImageFolder或标注文件检测到的类别列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> detectedClassNames = new();

        /// <summary>
        /// 检测任务从COCO标注文件检测到的类别
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> detectedDetectionClassNames = new();

        /// <summary>
        /// 学习率集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<float> learningRates;
        /// <summary>
        /// 选择的学习率
        /// </summary>
        [ObservableProperty]
        private float selectedLearningRate;
        /// <summary>
        /// 批处理大小集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<int> batchSizes;
        /// <summary>
        /// 选择的批处理大小
        /// </summary>
        [ObservableProperty]
        private int selectedBatchSize;
        /// <summary>
        /// 训练轮数
        /// </summary>
        private int epochs;
        public int Epochs
        {
            get => epochs;
            set
            {
                SetProperty(ref epochs, value);
                OnPropertyChanged(nameof(Epochs));
            }
        }
        /// <summary>
        /// 优化器集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> optimizers;
        /// <summary>
        /// 选择的优化器
        /// </summary>
        [ObservableProperty]
        private string selectedOptimizer;
        /// <summary>
        /// 验证集比例是否可用（未指定验证集时可用）
        /// </summary>
        [ObservableProperty]
        private bool isValidationSplitEnabled = true;
        /// <summary>
        /// 验证集比例集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<float> validationSetRates;
        /// <summary>
        /// 选择的验证集比例
        /// </summary>
        [ObservableProperty]
        private float selectedValidationSetRate;
        /// <summary>
        /// 学习率调度策略集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> schedulingStrategies;
        /// <summary>
        /// 选择的学习率调度策略
        /// </summary>
        [ObservableProperty]
        private string? selectedStrategy;
        /// <summary>
        /// 权重衰减
        /// </summary>
        [ObservableProperty]
        private float weightDecay;
        /// <summary>
        /// 早停轮数
        /// </summary>
        private int earlyStopRound;
        public int EarlyStopRound
        {
            get => earlyStopRound;
            set
            {
                SetProperty(ref earlyStopRound, value);
                OnPropertyChanged(nameof(EarlyStopRound));
            }
        }
        /// <summary>
        /// 早停阈值
        /// </summary>
        [ObservableProperty]
        private float earlyStopDelta;
        /// <summary>
        /// 损失函数类型集合
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<string> lossFunctionTypes;
        
        /// <summary>
        /// 损失函数描述
        /// </summary>
        [ObservableProperty]
        public string? lossFunctionDescribe;
        
        /// <summary>
        /// 选择的损失函数
        /// </summary>
        [ObservableProperty]
        public string? selectedLossFunction;
        
        partial void OnSelectedLossFunctionChanged(string? value)
        {
            UpdateLossFunctionUI(value);
        }
        
        /// <summary>
        /// 权重
        /// </summary>
        [ObservableProperty]
        public string? weight;
        
        partial void OnWeightChanged(string? value)
        {
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 正样本权重
        /// </summary>
        [ObservableProperty]
        public string? pos_weight;
        
        partial void OnPos_weightChanged(string? value)
        {
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 标签平滑因子
        /// </summary>
        [ObservableProperty]
        public double? labelSmoothing;
        
        partial void OnLabelSmoothingChanged(double? value)
        {
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 平滑L1损失函数的beta参数
        /// </summary>
        [ObservableProperty]
        public double? beta;
        
        partial void OnBetaChanged(double? value)
        {
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 损失计算方式集合
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<string> reductions;

        /// <summary>
        /// 选择的损失计算方式
        /// </summary>
        [ObservableProperty]
        public string? selectedReduction;

        partial void OnSelectedReductionChanged(string? value)
        {
            UpdateCodePreview();
        }

        /// <summary>
        /// KL散度损失函数的reduction参数集合
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<string> kLDivLoss_Reductions;

        /// <summary>
        /// 选择的KL散度损失函数
        /// </summary>
        [ObservableProperty]
        public string selectedKLDivLoss_Reduction;

        partial void OnSelectedKLDivLoss_ReductionChanged(string value)
        {
            UpdateCodePreview();
        }
        

        /// <summary>
        /// 代码预览
        /// </summary>
        [ObservableProperty]
        public string? codePreview;
        /// <summary>
        /// 是否可显示下一步按钮
        /// </summary>
        [ObservableProperty]
        private bool isVisibleNextStep = false;
        /// <summary>
        /// 随机水平反转是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomHorizonFlipChecked = false;
        /// <summary>
        /// 随机垂直反转是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomVerticalFlipChecked = false;
        /// <summary>
        /// 随机旋转是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomRotationChecked = false;
        /// <summary>
        /// 随机亮度是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomBrightnessChecked = false;
        /// <summary>
        /// 随机对比度是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomContrastChecked = false;
        /// <summary>
        /// 随机缩放是否选中
        /// </summary>
        [ObservableProperty]
        private bool randomZoomChecked = false;

        // 检测任务特有的数据增强
        /// <summary>
        /// 随机尺度变换是否选中（检测专用）
        /// </summary>
        [ObservableProperty]
        private bool randomScaleChecked = false;
        
        /// <summary>
        /// 随机色调饱和度是否选中（检测专用）
        /// </summary>
        [ObservableProperty]
        private bool randomHueSaturationChecked = false;

        // 检测任务特有属性
        /// <summary>
        /// 训练图像路径
        /// </summary>
        [ObservableProperty]
        private string? detectionTrainSetPath;

        /// <summary>
        /// 验证图像路径
        /// </summary>
        [ObservableProperty]
        private string? detectionValidationSetPath;

        partial void OnDetectionValidationSetPathChanged(string? value)
        {
            UpdateValidationSplitEnabled();
        }

        /// <summary>
        /// 训练标注文件路径
        /// </summary>
        [ObservableProperty]
        private string? trainAnnotationPath;

        /// <summary>
        /// 验证标注文件路径
        /// </summary>
        [ObservableProperty]
        private string? valAnnotationPath;

        /// <summary>
        /// 标注格式选择
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> annotationFormats = ["coco", "yolo", "pascal_voc"];

        [ObservableProperty]
        private string selectedAnnotationFormat = "coco";

        // 检测损失函数权重配置
        [ObservableProperty]
        private float rpnClassificationWeight = 1.0f;

        [ObservableProperty]
        private float rpnBoxRegressionWeight = 1.0f;

        [ObservableProperty]
        private float roiClassificationWeight = 1.0f;

        [ObservableProperty]
        private float roiBoxRegressionWeight = 1.0f;

        [ObservableProperty]
        private float? focalLossAlpha = 0.25f; // RetinaNet专用

        [ObservableProperty]
        private float? focalLossGamma = 2.0f; // RetinaNet专用

        [ObservableProperty]
        private string iouLossType = "iou"; // "iou", "giou", "diou", "ciou"

        [ObservableProperty]
        private float maskWeight = 1.0f; // Mask R-CNN专用

        [ObservableProperty]
        private float keypointWeight = 1.0f; // Keypoint R-CNN专用

        // UI控制属性
        [ObservableProperty]
        private bool isDetectionTask = false;

        [ObservableProperty]
        private bool isShowDetectionParameters = false;

        [ObservableProperty]
        private bool isShowFocalLossParameters = false; // RetinaNet专用

        [ObservableProperty]
        private bool isShowMaskParameters = false; // Mask R-CNN专用

        [ObservableProperty]
        private bool isShowKeypointParameters = false; // Keypoint R-CNN专用
        #endregion

        #region 命令
        [RelayCommand]
        private void Loaded()
        {
            UpdateUIForTaskType();
            if (App.TrainModel?.TaskType == "classification")
            {
                ClassifyTrainSetPath = App.TrainModel?.Classification?.TrainDataPath;
                ClassifyValidationSetPath = App.TrainModel?.Classification?.ValDataPath;
                ClassifyAnnotationPath = App.TrainModel?.Classification?.TrainAnnotationPath;

                // 如果已有标注文件，加载类别信息
                if (!string.IsNullOrEmpty(ClassifyAnnotationPath) && File.Exists(ClassifyAnnotationPath))
                {
                    LoadClassNamesFromAnnotation(ClassifyAnnotationPath);
                }

                // Load data augmentation settings
                if (App.TrainModel?.Classification?.DataAugmentation != null)
                {
                    RandomHorizonFlipChecked = App.TrainModel.Classification.DataAugmentation.RandomHorizonFlip;
                    RandomVerticalFlipChecked = App.TrainModel.Classification.DataAugmentation.RandomVerticalFlip;
                    RandomRotationChecked = App.TrainModel.Classification.DataAugmentation.RandomRotation;
                    RandomBrightnessChecked = App.TrainModel.Classification.DataAugmentation.RandomBrightness;
                    RandomContrastChecked = App.TrainModel.Classification.DataAugmentation.RandomContrast;
                    RandomZoomChecked = App.TrainModel.Classification.DataAugmentation.RandomZoom;
                }

                // Load validation split
                if (App.TrainModel?.Classification?.ValidationSplit > 0)
                {
                    SelectedValidationSetRate = App.TrainModel.Classification.ValidationSplit;
                }
            }
            else if(App.TrainModel?.TaskType == "detection")
            {
                DetectionTrainSetPath = App.TrainModel?.Detection?.TrainImagesPath;
                DetectionValidationSetPath = App.TrainModel?.Detection?.ValImagesPath;
                TrainAnnotationPath = App.TrainModel?.Detection?.TrainAnnotationPath;
                ValAnnotationPath = App.TrainModel?.Detection?.ValAnnotationPath;

                // 加载检测任务的数据增强配置
                if (App.TrainModel?.Detection?.DataAugmentation != null)
                {
                    RandomHorizonFlipChecked = App.TrainModel.Detection.DataAugmentation.RandomHorizonFlip;
                    RandomBrightnessChecked = App.TrainModel.Detection.DataAugmentation.RandomBrightness;
                    RandomContrastChecked = App.TrainModel.Detection.DataAugmentation.RandomContrast;
                    RandomScaleChecked = App.TrainModel.Detection.DataAugmentation.RandomScale;
                    RandomHueSaturationChecked = App.TrainModel.Detection.DataAugmentation.RandomHueSaturation;
                }

                // 如果已有训练标注文件，加载类别信息
                if (!string.IsNullOrEmpty(TrainAnnotationPath) && File.Exists(TrainAnnotationPath))
                {
                    var categories = ParseCocoCategories(TrainAnnotationPath);
                    if (categories.Count > 0)
                    {
                        DetectedDetectionClassNames.Clear();
                        foreach (var category in categories)
                        {
                            DetectedDetectionClassNames.Add(category);
                        }
                    }
                }

                // Load annotation format
                if (!string.IsNullOrEmpty(App.TrainModel?.Detection?.AnnotationFormat))
                {
                    SelectedAnnotationFormat = App.TrainModel.Detection.AnnotationFormat;
                }

                if (App.TrainModel != null)
                {
                    IsDetectionTask = App.TrainModel.TaskType == "detection";
                }
            }
        }
        [RelayCommand]
        private async Task EditPath(TextBox textbox)
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow as MainWindow
                : null;
            if (mainWindow != null)
            {
                var toplevel = TopLevel.GetTopLevel(mainWindow);
                if (toplevel != null)
                {
                    // 根据控件的Tag属性判断是选择目录还是文件
                    var pathType = textbox.Tag as string;
                    var isDirectory = true; // 默认为目录选择

                    if (!string.IsNullOrEmpty(pathType))
                    {
                        isDirectory = pathType.Equals("Directory", StringComparison.OrdinalIgnoreCase);
                    }

                    if (isDirectory)
                    {
                        // 选择目录
                        var folders = await toplevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions()
                        {
                            AllowMultiple = false,
                            Title = "选择目录",
                        });
                        if (folders.Count > 0)
                        {
                            var selectedFolder = folders[0].TryGetLocalPath();
                            textbox.Text = selectedFolder;

                            // 检查是否是分类训练集目录，进行ImageFolder格式检测
                            if (textbox.Name == "TrainDir_Tb" && !IsDetectionTask)
                            {
                                await CheckAndHandleImageFolderFormat(selectedFolder ?? string.Empty, showWarning: true);
                            }
                        }
                    }
                    else
                    {
                        // 选择文件
                        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                        {
                            AllowMultiple = false,
                            Title = "选择文件",
                        });
                        if (files.Count > 0)
                        {
                            var selectedPath = files[0].TryGetLocalPath();
                            textbox.Text = selectedPath;

                            // 处理标注文件选择
                            if (textbox.Name == "ClassifyAnnotation_Tb" && !IsDetectionTask)
                            {
                                // 分类标注文件
                                await HandleAnnotationFileSelected(selectedPath ?? string.Empty);
                            }
                            else if (textbox.Name == "TrainAnnotation_Tb" && IsDetectionTask)
                            {
                                // 检测训练标注文件
                                await HandleDetectionAnnotationSelected(selectedPath ?? string.Empty);
                            }
                            else if (textbox.Name == "ValAnnotation_Tb" && IsDetectionTask)
                            {
                                // 检测验证标注文件
                                await HandleDetectionAnnotationSelected(selectedPath ?? string.Empty);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(ClassifyValidationSetPath))
                    {
                        UpdateValidationSplitEnabled();
                    }
                }
            }
        }
        [RelayCommand]
        private void ClearPath(TextBox textbox)
        {
            textbox.Text = string.Empty;
            if (string.IsNullOrEmpty(ClassifyValidationSetPath))
            {
                UpdateValidationSplitEnabled();
            }
        }
        /// <summary>
        /// 跳转到下一个选项卡
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [RelayCommand]
        private static async Task GoToNextTab(UserControl o)
        {
            if (o.Parent != null && o.Parent.Parent is TabControl control)
            {
                await Dispatcher.UIThread.InvokeAsync(() => control.SelectedIndex = 3);
            }
        }
        /// <summary>
        /// 获取权重数组
        /// </summary>
        /// <param name="weight"></param>
        /// <returns></returns>
        private double[] GetWeightArray(string weight)
        {
            string pattern = @"^[0-9,.]+$";
            if (weight != null && Regex.IsMatch(weight, pattern))
            {
                var array = weight.Split(',');
                if (array.All(t => double.TryParse(t, out _)))
                {
                    var weightArray = array.Where(t => t != string.Empty).Select(t => double.Parse(t))
                        .ToArray();
                    return weightArray;
                }
                return [];
            }
            else
            {
                return [];
            }
        }
        
        /// <summary>
        /// 更新损失函数UI（描述和reduction默认值）
        /// </summary>
        private void UpdateLossFunctionUI(string? lossType)
        {
            switch (lossType)
            {
                case "CrossEntropyLoss":
                    LossFunctionDescribe = "标准多分类损失函数，最常用于图像分类任务";
                    // 如果当前 reduction 不在 Reductions 集合中，重置为 mean
                    if (SelectedReduction == null || !Reductions.Contains(SelectedReduction))
                    {
                        SelectedReduction = "mean";
                    }
                    break;
                    
                case "BCEWithLogitsLoss":
                    LossFunctionDescribe = "二分类交叉熵损失函数，适用于二分类任务";
                    // 如果当前 reduction 不在 Reductions 集合中，重置为 mean
                    if (SelectedReduction == null || !Reductions.Contains(SelectedReduction))
                    {
                        SelectedReduction = "mean";
                    }
                    break;
                    
                case "KLDivLoss":
                    LossFunctionDescribe = "KL散度损失函数，适用于分布预测任务";
                    // 如果当前 reduction 不在 KLDivLoss_Reductions 集合中，重置为 batchmean
                    if (SelectedKLDivLoss_Reduction == null || !KLDivLoss_Reductions.Contains(SelectedKLDivLoss_Reduction))
                    {
                        SelectedKLDivLoss_Reduction = "batchmean";
                    }
                    break;
                    
                default:
                    LossFunctionDescribe = string.Empty;
                    break;
            }
            
            // 更新代码预览
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 更新损失函数代码预览
        /// </summary>
        private void UpdateCodePreview()
        {
            if (string.IsNullOrEmpty(SelectedLossFunction))
                return;
                
            ClassifyLossConfig lossFunctionModel;
            
            switch (SelectedLossFunction)
            {
                case "CrossEntropyLoss":
                    lossFunctionModel = new ClassifyLossConfig
                    {
                        type = SelectedLossFunction,
                        args = new Params
                        {
                            weight = !string.IsNullOrEmpty(Weight) ? GetWeightArray(Weight) : null,
                            pos_weight = null,
                            Beta = null,
                            label_smoothing = LabelSmoothing,
                            reduction = SelectedReduction
                        }
                    };
                    break;
                    
                case "BCEWithLogitsLoss":
                    lossFunctionModel = new ClassifyLossConfig
                    {
                        type = SelectedLossFunction,
                        args = new Params
                        {
                            weight = !string.IsNullOrEmpty(Weight) ? GetWeightArray(Weight) : null,
                            pos_weight = !string.IsNullOrEmpty(Pos_weight) ? GetWeightArray(Pos_weight) : null,
                            reduction = SelectedReduction,
                            Beta = null,
                            label_smoothing = null
                        }
                    };
                    break;
                    
                case "KLDivLoss":
                    lossFunctionModel = new ClassifyLossConfig
                    {
                        type = SelectedLossFunction,
                        args = new Params
                        {
                            weight = null,
                            pos_weight = null,
                            label_smoothing = null,
                            Beta = null,
                            reduction = SelectedKLDivLoss_Reduction
                        }
                    };
                    break;
                    
                default:
                    return;
            }
            
            // 生成JSON预览
            CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            });
        }
        
        /// <summary>
        /// 保存配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SaveConfig()
        {
            // 分类任务验证
            if (!IsDetectionTask)
            {
                if (!ValidateClassificationTrainSet())
                {
                    await ShowClassificationFormatWarning();
                    return;
                }
                
                if (DetectedClassNames.Count == 0)
                {
                    var messageBox = MessageBoxManager.GetMessageBoxStandard(
                        "错误",
                        "未检测到任何类别，请检查数据集配置",
                        ButtonEnum.Ok,
                        Icon.Error);
                    await messageBox.ShowWindowDialogAsync(MainWindow);
                    return;
                }
            }

            // 保存通用配置
            App.TrainModel.LearningRate = SelectedLearningRate;
            App.TrainModel.LrScheduler = SelectedStrategy;
            App.TrainModel.WeightDecay = WeightDecay;
            App.TrainModel.BatchSize = SelectedBatchSize;
            App.TrainModel.Optimizer = SelectedOptimizer;
            App.TrainModel.Epochs = Epochs;
            App.TrainModel.EarlyStoppingRounds = EarlyStopRound;
            App.TrainModel.EarlyStoppingDelta = EarlyStopDelta;
            App.TrainModel.CustomModelName = string.IsNullOrWhiteSpace(CustomModelName) ? App.TrainModel.PretrainedModel : CustomModelName;

            // 根据任务类型保存特定配置
            if (IsDetectionTask)
            {
                // 检测任务配置 - 使用新结构
                App.TrainModel.Detection ??= new DetectionConfig();
                App.TrainModel.Detection.TrainImagesPath = DetectionTrainSetPath;
                App.TrainModel.Detection.ValImagesPath = DetectionValidationSetPath;
                App.TrainModel.Detection.TrainAnnotationPath = TrainAnnotationPath;
                App.TrainModel.Detection.ValAnnotationPath = ValAnnotationPath;
                App.TrainModel.Detection.AnnotationFormat = SelectedAnnotationFormat;

                // 检测任务数据增强配置
                App.TrainModel.Detection.DataAugmentation.RandomHorizonFlip = RandomHorizonFlipChecked;
                App.TrainModel.Detection.DataAugmentation.RandomBrightness = RandomBrightnessChecked;
                App.TrainModel.Detection.DataAugmentation.RandomContrast = RandomContrastChecked;
                App.TrainModel.Detection.DataAugmentation.RandomScale = RandomScaleChecked;
                App.TrainModel.Detection.DataAugmentation.RandomHueSaturation = RandomHueSaturationChecked;

                // 检测损失函数配置
                App.TrainModel.Detection.DetectionLoss = new DetectionLossConfig
                {
                    RpnClassificationWeight = RpnClassificationWeight,
                    RpnBoxRegressionWeight = RpnBoxRegressionWeight,
                    RoIClassificationWeight = RoiClassificationWeight,
                    RoIBoxRegressionWeight = RoiBoxRegressionWeight,
                    FocalLossAlpha = FocalLossAlpha,
                    FocalLossGamma = FocalLossGamma,
                    IouLossType = IouLossType,
                    MaskWeight = MaskWeight,
                    KeypointWeight = KeypointWeight
                };
                
                // 从训练标注文件中读取类别数（如果存在）
                if (!string.IsNullOrEmpty(TrainAnnotationPath) && File.Exists(TrainAnnotationPath))
                {
                    try
                    {
                        var jsonText = File.ReadAllText(TrainAnnotationPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                        
                        if (doc.RootElement.TryGetProperty("categories", out var categories))
                        {
                            var categoryCount = categories.GetArrayLength();
                            App.TrainModel.NumClasses = categoryCount;
                            Log.Information($"从检测标注文件读取类别数: {categoryCount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"读取检测类别数失败: {ex.Message}，请手动设置类别数");
                    }
                }
            }
            else
            {
                // 分类任务配置 - 使用新结构
                App.TrainModel.Classification ??= new ClassificationConfig();
                App.TrainModel.Classification.TrainDataPath = ClassifyTrainSetPath;
                App.TrainModel.Classification.ValDataPath = ClassifyValidationSetPath;
                App.TrainModel.Classification.TrainAnnotationPath = ClassifyAnnotationPath;

                // 根据数据集格式设置类别数
                if (IsImageFolderFormat)
                {
                    // ImageFolder格式：从目录结构获取类别数
                    App.TrainModel.NumClasses = DetectedClassNames.Count;
                    Log.Information($"使用ImageFolder格式，类别数: {DetectedClassNames.Count}");
                }
                else if (!string.IsNullOrEmpty(ClassifyAnnotationPath) && File.Exists(ClassifyAnnotationPath))
                {
                    // 标注文件模式：从标注文件获取类别数
                    App.TrainModel.NumClasses = DetectedClassNames.Count;
                    Log.Information($"使用标注文件模式，类别数: {DetectedClassNames.Count}");
                }

                // 验证集分割比例（仅在没有单独验证集时使用）
                App.TrainModel.Classification.ValidationSplit = string.IsNullOrEmpty(ClassifyValidationSetPath) 
                    ? SelectedValidationSetRate 
                    : 0;

                // 数据增强配置
                App.TrainModel.Classification.DataAugmentation.RandomHorizonFlip = RandomHorizonFlipChecked;
                App.TrainModel.Classification.DataAugmentation.RandomVerticalFlip = RandomVerticalFlipChecked;
                App.TrainModel.Classification.DataAugmentation.RandomRotation = RandomRotationChecked;
                App.TrainModel.Classification.DataAugmentation.RandomBrightness = RandomBrightnessChecked;
                App.TrainModel.Classification.DataAugmentation.RandomContrast = RandomContrastChecked;
                App.TrainModel.Classification.DataAugmentation.RandomZoom = RandomZoomChecked;

                // 分类损失函数配置
                App.TrainModel.Classification.LossFunction = new ClassifyLossConfig 
                {
                    type = SelectedLossFunction,
                    args = new Params
                    {
                        weight = !string.IsNullOrEmpty(Weight) ? GetWeightArray(Weight) : null,
                        pos_weight = !string.IsNullOrEmpty(Pos_weight) ? GetWeightArray(Pos_weight) : null,
                        label_smoothing = LabelSmoothing,
                        Beta = Beta,
                        reduction = SelectedReduction
                    }
                };
            }

            // 同名文件警告检查
            try
            {
                var outputDir = App.TrainModel.ModelOutputPath;
                var modelBaseName = string.IsNullOrWhiteSpace(App.TrainModel.CustomModelName) ? App.TrainModel.PretrainedModel : App.TrainModel.CustomModelName;
                if (!string.IsNullOrWhiteSpace(outputDir) && !string.IsNullOrWhiteSpace(modelBaseName))
                {
                    var pthPath = Path.Combine(outputDir, modelBaseName + ".pth");
                    var ptPath = Path.Combine(outputDir, modelBaseName + ".pt");
                    if (File.Exists(pthPath) || File.Exists(ptPath))
                    {
                        var box = MessageBoxManager.GetMessageBoxStandard(
                            "警告",
                            $"模型输出目录中已存在同名文件: {modelBaseName}.pth 或 {modelBaseName}.pt\n继续保存配置并训练可能会覆盖旧文件。",
                            ButtonEnum.Ok,
                            Icon.Warning);
                        await box.ShowWindowDialogAsync(MainWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"检查同名文件时发生错误: {ex.Message}");
            }

            string jsonStr = JsonConvert.SerializeObject(App.TrainModel, Formatting.Indented);
            string configPath = Path.Combine(App.ConfigFolderPath, "ModelParam.json");
            await File.WriteAllTextAsync(configPath, jsonStr);
            IsVisibleNextStep = true;
        }

        /// <summary>
        /// 根据任务类型更新UI显示
        /// </summary>
        private void UpdateUIForTaskType()
        {
            IsDetectionTask = App.TrainModel?.TaskType == "detection";
            IsShowDetectionParameters = IsDetectionTask;

            if (IsDetectionTask)
            {
                // 检测任务的UI调整
                UpdateDetectionModelSpecificUI();
            }
        }

        /// <summary>
        /// 根据检测模型类型更新特定UI
        /// </summary>
        private void UpdateDetectionModelSpecificUI()
        {
            if (App.TrainModel?.PretrainedModel == null) return;

            var modelName = App.TrainModel.PretrainedModel.ToLower();

            // RetinaNet 显示 Focal Loss 参数
            IsShowFocalLossParameters = modelName.Contains("retinanet");

            // Mask R-CNN 显示掩码参数
            IsShowMaskParameters = modelName.Contains("maskrcnn");

            // Keypoint R-CNN 显示关键点参数
            IsShowKeypointParameters = modelName.Contains("keypointrcnn");
        }

        /// <summary>
        /// 更新验证集比例是否可用
        /// 逻辑：当用户指定了验证集目录时，验证集比例不可用
        /// </summary>
        private void UpdateValidationSplitEnabled()
        {
            if (IsDetectionTask)
            {
                // 检测任务：检查验证集图像路径和标注路径
                IsValidationSplitEnabled = string.IsNullOrWhiteSpace(DetectionValidationSetPath) || 
                                          string.IsNullOrWhiteSpace(ValAnnotationPath);
            }
            else
            {
                // 分类任务：检查验证集路径
                IsValidationSplitEnabled = string.IsNullOrWhiteSpace(ClassifyValidationSetPath);
            }
        }

        /// <summary>
        /// 检查并处理ImageFolder格式的训练集
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="showWarning">是否显示警告对话框（手动选择时为true，自动触发时为false）</param>
        private async Task CheckAndHandleImageFolderFormat(string folderPath, bool showWarning = true)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    return;

                var subDirs = Directory.GetDirectories(folderPath);
                
                if (subDirs.Length < 2)
                {
                    IsImageFolderFormat = false;
                    DetectedClassNames.Clear();
                    if (showWarning)
                    {
                        await ShowClassificationFormatWarning();
                    }
                    return;
                }

                var validClasses = new List<string>();
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

                foreach (var dir in subDirs)
                {
                    var dirName = Path.GetFileName(dir);
                    var hasImages = Directory.EnumerateFiles(dir)
                        .Any(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()));

                    if (hasImages)
                    {
                        validClasses.Add(dirName);
                    }
                }

                if (validClasses.Count >= 2)
                {
                    IsImageFolderFormat = true;
                    DetectedClassNames.Clear();
                    foreach (var className in validClasses.OrderBy(c => c))
                    {
                        DetectedClassNames.Add(className);
                    }
                    App.TrainModel.NumClasses = validClasses.Count;
                    ClassifyAnnotationPath = null;
                    
                    Log.Information($"检测到ImageFolder格式，{validClasses.Count} 个类别: {string.Join(", ", validClasses)}");
                    
                    // 自动计算类别权重
                    await CalculateAndSetClassWeights();
                }
                else
                {
                    IsImageFolderFormat = false;
                    DetectedClassNames.Clear();
                    if (showWarning)
                    {
                        await ShowClassificationFormatWarning();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查ImageFolder格式时出错");
                IsImageFolderFormat = false;
                if (showWarning)
                {
                    await ShowClassificationFormatWarning();
                }
            }
        }

        /// <summary>
        /// 显示分类格式警告
        /// </summary>
        private async Task ShowClassificationFormatWarning()
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "数据集格式不符合要求",
                "训练集目录不符合ImageFolder格式（按类别分文件夹）。\n\n" +
                "请执行以下操作之一：\n" +
                "1. 调整目录结构为ImageFolder格式\n" +
                "2. 提供分类标注文件（TXT格式）",
                ButtonEnum.Ok,
                Icon.Warning);
            
            await messageBox.ShowWindowDialogAsync(MainWindow);
        }

        /// <summary>
        /// 处理标注文件选择
        /// </summary>
        private async Task HandleAnnotationFileSelected(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var (isValid, classNames, imageCount) = await Task.Run(() => 
                    ValidateClassificationAnnotationFile(filePath, ClassifyTrainSetPath));

                if (!isValid)
                {
                    Log.Warning("标注文件格式不正确或无法与训练集图片匹配");
                    var messageBox = MessageBoxManager.GetMessageBoxStandard(
                        "标注文件验证失败",
                        "标注文件格式不正确或无法与训练集图片匹配。\n\n" +
                        "请确保：\n" +
                        "1. TXT文件每行格式：imageName className\n" +
                        "2. 至少有1张图片能在训练集中找到",
                        ButtonEnum.Ok,
                        Icon.Warning);
                    await messageBox.ShowWindowDialogAsync(MainWindow);
                    return;
                }

                IsImageFolderFormat = false;
                DetectedClassNames.Clear();
                foreach (var className in classNames.OrderBy(c => c))
                {
                    DetectedClassNames.Add(className);
                }

                App.TrainModel.NumClasses = classNames.Count;

                Log.Information($"从标注文件加载 {classNames.Count} 个类别，匹配 {imageCount} 张图片");
                
                // 自动计算类别权重
                await CalculateAndSetClassWeights();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理标注文件时出错");
            }
        }

        /// <summary>
        /// 验证分类标注文件
        /// </summary>
        private (bool isValid, HashSet<string> classNames, int imageCount) ValidateClassificationAnnotationFile(
            string annotationPath, string? imageDir)
        {
            var classNames = new HashSet<string>();
            var matchedCount = 0;
            
            try
            {
                if (string.IsNullOrEmpty(annotationPath) || !File.Exists(annotationPath))
                    return (false, classNames, 0);

                var lines = File.ReadAllLines(annotationPath);
                
                HashSet<string>? imageFiles = null;
                if (!string.IsNullOrEmpty(imageDir) && Directory.Exists(imageDir))
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                    imageFiles = new HashSet<string>(
                        Directory.EnumerateFiles(imageDir, "*.*", SearchOption.AllDirectories)
                            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                            .Select(f => Path.GetFileNameWithoutExtension(f)),
                        StringComparer.OrdinalIgnoreCase
                    );
                }

                foreach (var line in lines)
                {
                    var parts = line.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var imageName = parts[0];
                        var className = parts[1];

                        classNames.Add(className);

                        if (imageFiles != null)
                        {
                            if (imageFiles.Contains(imageName))
                            {
                                matchedCount++;
                            }
                        }
                        else
                        {
                            matchedCount++;
                        }
                    }
                }

                bool isValid = classNames.Count > 0 && matchedCount > 0;
                
                return (isValid, classNames, matchedCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "验证标注文件失败");
                return (false, classNames, 0);
            }
        }

        /// <summary>
        /// 从标注文件加载类别名称
        /// </summary>
        private void LoadClassNamesFromAnnotation(string annotationPath)
        {
            try
            {
                var (isValid, classNames, imageCount) = ValidateClassificationAnnotationFile(
                    annotationPath, ClassifyTrainSetPath);

                if (isValid)
                {
                    DetectedClassNames.Clear();
                    foreach (var className in classNames.OrderBy(c => c))
                    {
                        DetectedClassNames.Add(className);
                    }
                    
                    // 自动计算类别权重（使用async void包装）
                    _ = CalculateAndSetClassWeights();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "从标注文件加载类别失败");
            }
        }

        /// <summary>
        /// 处理检测标注文件选择
        /// </summary>
        private async Task HandleDetectionAnnotationSelected(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var categories = await Task.Run(() => ParseCocoCategories(filePath));

                if (categories.Count > 0)
                {
                    DetectedDetectionClassNames.Clear();
                    foreach (var category in categories.OrderBy(c => c))
                    {
                        DetectedDetectionClassNames.Add(category);
                    }

                    App.TrainModel.NumClasses = categories.Count;

                    Log.Information($"从COCO标注文件加载 {categories.Count} 个类别");
                }
                else
                {
                    Log.Warning("COCO标注文件中未找到类别信息");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理COCO标注文件时出错");
            }
        }

        /// <summary>
        /// 解析COCO格式标注文件中的类别
        /// </summary>
        private List<string> ParseCocoCategories(string cocoFilePath)
        {
            var categories = new List<string>();
            
            try
            {
                var jsonText = File.ReadAllText(cocoFilePath);
                using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                
                if (doc.RootElement.TryGetProperty("categories", out var categoriesElement))
                {
                    foreach (var category in categoriesElement.EnumerateArray())
                    {
                        if (category.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                categories.Add(name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "解析COCO类别失败");
            }
            
            return categories;
        }

        /// <summary>
        /// 计算并设置类别权重
        /// </summary>
        private async Task CalculateAndSetClassWeights()
        {
            try
            {
                if (App.TrainModel?.TaskType != "classification")
                    return;

                Dictionary<string, int> classCounts = new();

                // 情况1: ImageFolder格式
                if (IsImageFolderFormat && !string.IsNullOrEmpty(ClassifyTrainSetPath))
                {
                    await Task.Run(() =>
                    {
                        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                        var temps = new List<string>(DetectedClassNames);
                        foreach (var className in temps)
                        {
                            var classDir = Path.Combine(ClassifyTrainSetPath, className);
                            if (Directory.Exists(classDir))
                            {
                                var count = Directory.EnumerateFiles(classDir, "*.*", SearchOption.AllDirectories)
                                    .Count(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()));
                                classCounts[className] = count;
                            }
                        }
                    });
                }
                // 情况2: 标注文件格式
                else if (!string.IsNullOrEmpty(ClassifyAnnotationPath) && File.Exists(ClassifyAnnotationPath))
                {
                    await Task.Run(() =>
                    {
                        var lines = File.ReadAllLines(ClassifyAnnotationPath);
                        foreach (var line in lines)
                        {
                            var parts = line.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2)
                            {
                                var className = parts[1];
                                if (!classCounts.ContainsKey(className))
                                    classCounts[className] = 0;
                                classCounts[className]++;
                            }
                        }
                    });
                }
                else
                {
                    return;
                }

                // 如果没有统计到数据，退出
                if (classCounts.Count == 0)
                {
                    Log.Warning("无法统计类别样本数量");
                    return;
                }

                // 计算权重（逆频率加权）
                int totalSamples = classCounts.Values.Sum();
                int numClasses = classCounts.Count;
                var weights = new List<double>();
                
                var classNameList = DetectedClassNames.ToList();
                foreach (var className in classNameList)
                {
                    if (classCounts.TryGetValue(className, out int count) && count > 0)
                    {
                        // weight = total_samples / (num_classes * class_count)
                        double weight = (double)totalSamples / (numClasses * count);
                        weights.Add(Math.Round(weight, 4));
                    }
                    else
                    {
                        weights.Add(1.0);
                    }
                }

                // 根据损失函数类型填充不同字段
                string weightsString = string.Join(",", weights);
                
                if (SelectedLossFunction == "BCEWithLogitsLoss")
                {
                    // BCEWithLogitsLoss: 填充到 pos_weight，weight 留空
                    Pos_weight = weightsString;
                    Weight = string.Empty;
                    Log.Information($"自动计算类别权重完成 - 总样本数: {totalSamples}, 类别数: {numClasses}");
                    Log.Information($"类别分布: {string.Join(", ", classCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    Log.Information($"Pos_weight (正样本权重): {Pos_weight}");
                }
                else if (SelectedLossFunction == "CrossEntropyLoss")
                {
                    // CrossEntropyLoss: 填充到 weight
                    Weight = weightsString;
                    Pos_weight = string.Empty;
                    Log.Information($"自动计算类别权重完成 - 总样本数: {totalSamples}, 类别数: {numClasses}");
                    Log.Information($"类别分布: {string.Join(", ", classCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    Log.Information($"Weight (类别权重): {Weight}");
                }
                else
                {
                    // 其他损失函数（如 KLDivLoss）不支持权重
                    Weight = string.Empty;
                    Pos_weight = string.Empty;
                    Log.Information($"当前损失函数 {SelectedLossFunction} 不支持类别权重");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "计算类别权重时出错");
            }
        }

        /// <summary>
        /// 验证分类训练集是否有效
        /// </summary>
        private bool ValidateClassificationTrainSet()
        {
            if (string.IsNullOrEmpty(ClassifyTrainSetPath))
                return false;

            if (!string.IsNullOrEmpty(ClassifyAnnotationPath) && File.Exists(ClassifyAnnotationPath))
                return true;

            if (!IsImageFolderFormat)
            {
                return false;
            }

            return true;
        }

        #endregion

    }
}
