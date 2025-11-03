using AutoTrainer.Models;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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
    public partial class ParameterConfigViewModel : ViewModelBase
    {
        public ParameterConfigViewModel()
        {
            LearningRates = [0.1f, 0.01f, 0.001f, 0.0001f];
            BatchSizes = [8, 16, 32, 64];
            Optimizers = ["Adam", "SGD"];
            ValidationSetRates = [0.1f, 0.2f, 0.3f];
            SchedulingStrategies = ["ReduceLROnPlateau", "StepLR"];
            LossFunctionTypes = ["CrossEntropyLoss", "BCELoss", "BCEWithLogitsLoss", "MSELoss", "L1Loss", "SmoothL1Loss", "KLDivLoss"];
            Reductions = ["mean", "sum", "none"];
            KLDivLoss_Reductions = ["mean", "sum", "none", "batchmean"];
            SelectedLearningRate = LearningRates[1];
            SelectedBatchSize = BatchSizes[1];
            SelectedValidationSetRate = ValidationSetRates[1];
            SelectedOptimizer = Optimizers[0];
            SelectedStrategy = SchedulingStrategies[0];
            SelectedLossFunction = LossFunctionTypes[0];
            Epochs = 25;
            EarlyStopRound = 5;
            EarlyStopDelta = 0.0001f;
            this.PropertyChanged += ParameterConfigViewModel_PropertyChanged;
        }

        private void ParameterConfigViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsVisibleNextStep")
            {
                IsVisibleNextStep = false;
            }
            if (e.PropertyName == "SelectedLossFunction" || e.PropertyName == "Weight" ||
                e.PropertyName == "SelectedReduction" || e.PropertyName == "LabelSmoothing" ||
                e.PropertyName == "Pos_weight" || e.PropertyName == "Beta" || e.PropertyName == "KLDivLoss_Reductions")
            {
                LossFunctionModel lossFunctionModel;

                switch (SelectedLossFunction)
                {
                    case "CrossEntropyLoss":
                        LossFunctionDescribe = "标准多分类损失函数，最常用于图像分类任务";
                        if (!string.IsNullOrEmpty(Weight))
                        {
                            lossFunctionModel = new LossFunctionModel
                            {
                                type = SelectedLossFunction,
                                args = new Params
                                {
                                    weight = GetWeightArray(Weight),
                                    pos_weight = null,
                                    Beta = null,
                                    label_smoothing = LabelSmoothing,
                                    reduction = SelectedReduction
                                }
                            };
                            CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                NullValueHandling = NullValueHandling.Ignore,
                            });
                        }
                        break;
                    case "BCELoss":
                        LossFunctionDescribe = "二分类交叉熵损失函数";
                        if (!string.IsNullOrEmpty(Weight))
                        {
                            lossFunctionModel = new LossFunctionModel
                            {
                                type = SelectedLossFunction,
                                args = new Params
                                {
                                    weight = GetWeightArray(Weight),
                                    pos_weight = null,
                                    Beta = null,
                                    label_smoothing = null,
                                    reduction = SelectedReduction
                                }
                            };
                            CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                NullValueHandling = NullValueHandling.Ignore,
                            });
                        }
                        break;
                    case "BCEWithLogitsLoss":
                        LossFunctionDescribe = "二分类交叉熵损失函数，适用于二分类任务";
                        if (!string.IsNullOrEmpty(Weight) && !string.IsNullOrEmpty(Pos_weight))
                        {
                            lossFunctionModel = new LossFunctionModel
                            {
                                type = SelectedLossFunction,
                                args = new Params
                                {
                                    weight = GetWeightArray(Weight),
                                    pos_weight = GetWeightArray(Pos_weight),
                                    reduction = SelectedReduction,
                                    Beta = null,
                                    label_smoothing = null
                                }
                            };
                            CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                NullValueHandling = NullValueHandling.Ignore,
                            });
                        }
                        break;
                    case "MSELoss":
                        LossFunctionDescribe = "均方误差损失函数，适用于回归任务";
                        lossFunctionModel = new LossFunctionModel
                        {
                            type = SelectedLossFunction,
                            args = new Params
                            {
                                weight = null,
                                pos_weight = null,
                                label_smoothing = null,
                                Beta = null,
                                reduction = SelectedReduction
                            }
                        };
                        CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                        });
                        break;
                    case "L1Loss":
                        LossFunctionDescribe = "L1损失函数，适用于回归任务";
                        lossFunctionModel = new LossFunctionModel
                        {
                            type = SelectedLossFunction,
                            args = new Params
                            {
                                weight = null,
                                pos_weight = null,
                                label_smoothing = null,
                                Beta = null,
                                reduction = SelectedReduction
                            }
                        };
                        CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                        });
                        break;
                    case "SmoothL1Loss":
                        LossFunctionDescribe = "平滑L1损失函数，适用于回归任务";
                        lossFunctionModel = new LossFunctionModel
                        {
                            type = SelectedLossFunction,
                            args = new Params
                            {
                                weight = null,
                                pos_weight = null,
                                label_smoothing = null,
                                Beta = Beta,
                                reduction = SelectedReduction
                            }
                        };
                        CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                        });
                        break;
                    case "KLDivLoss":
                        LossFunctionDescribe = "KL散度损失函数，适用于分布预测任务";
                        lossFunctionModel = new LossFunctionModel
                        {
                            type = SelectedLossFunction,
                            args = new Params
                            {
                                weight = null,
                                pos_weight = null,
                                label_smoothing = null,
                                Beta = null,
                                reduction = SelectedReduction
                            }
                        };
                        CodePreview = JsonConvert.SerializeObject(lossFunctionModel, new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                        });
                        break;
                }   
            }
        }
        #region 可绑定属性
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
        /// <summary>
        /// 权重
        /// </summary>
        [ObservableProperty]
        public string? weight;
        /// <summary>
        /// 正样本权重
        /// </summary>
        [ObservableProperty]
        public string? pos_weight;
        /// <summary>
        /// 标签平滑因子
        /// </summary>
        [ObservableProperty]
        public double? labelSmoothing;
        /// <summary>
        /// 平滑L1损失函数的beta参数
        /// </summary>
        [ObservableProperty]
        public double? beta;
        /// <summary>
        /// 损失计算方式
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<string> reductions;
        /// <summary>
        /// KL散度损失函数的reduction参数
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<string> kLDivLoss_Reductions;
        /// <summary>
        /// 选择的损失计算方式
        /// </summary>
        [ObservableProperty]
        public string? selectedReduction;
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
        #endregion

        #region 命令

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
        /// 保存配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SaveConfig()
        {
            App.TrainModel.LearningRate = SelectedLearningRate;
            App.TrainModel.LrScheduler = SelectedStrategy;
            App.TrainModel.WeightDecay = WeightDecay;
            App.TrainModel.BatchSize = SelectedBatchSize;
            App.TrainModel.Optimizer = SelectedOptimizer;
            App.TrainModel.Epochs = Epochs;
            App.TrainModel.EarlyStoppingRounds = EarlyStopRound;
            App.TrainModel.EarlyStoppingDelta = EarlyStopDelta;
            App.TrainModel.ValidationSplit = SelectedValidationSetRate;
            App.TrainModel.RandomHorizonFlipChecked = RandomHorizonFlipChecked;
            App.TrainModel.RandomVerticalFlipChecked = RandomVerticalFlipChecked;
            App.TrainModel.RandomRotationChecked = RandomRotationChecked;
            App.TrainModel.RandomBrightnessChecked = RandomBrightnessChecked;
            App.TrainModel.RandomContrastChecked = RandomContrastChecked;
            App.TrainModel.RandomZoomChecked = RandomZoomChecked;
            // 始终设置损失函数配置
            App.TrainModel.LossFunction = new LossFunctionModel
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
            string jsonStr = JsonConvert.SerializeObject(App.TrainModel, Formatting.Indented);
            string configPath = Path.Combine(App.ConfigFolderPath, "ModelParam.json");
            await File.WriteAllTextAsync(configPath, jsonStr);
            IsVisibleNextStep = true;
        }
        #endregion

    }
}
