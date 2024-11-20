using AutoTrainer.Models;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
            ValidationSetRates = [0.1, 0.2, 0.3];
            SchedulingStrategies = ["不使用", "Step", "Cosine"];
            SelectedLearningRate = LearningRates[0];
            SelectedBatchSize = BatchSizes[1];
            SelectedValidationSetRate = ValidationSetRates[1];
            SelectedOptimizer = Optimizers[0];
            SelectedStrategy = SchedulingStrategies[0];
            Epochs = 100;
            EarlyStopRound = 5;
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
        private int _epochs;
        public int Epochs
        {
            get => _epochs;
            set
            {
                SetProperty(ref _epochs, value);
                OnPropertyChanged(nameof(Epochs));
                ShowNextBtn();
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
        private ObservableCollection<double> validationSetRates;
        /// <summary>
        /// 选择的验证集比例
        /// </summary>
        [ObservableProperty]
        private double selectedValidationSetRate;
        /// <summary>
        /// 学习率调度策略集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> schedulingStrategies;
        /// <summary>
        /// 选择的学习率调度策略
        /// </summary>
        [ObservableProperty]
        private string selectedStrategy;
        /// <summary>
        /// 权重衰减
        /// </summary>
        [ObservableProperty]
        private double weightDecay;
        /// <summary>
        /// 早停轮数
        /// </summary>
        private int _earlyStopRound;
        public int EarlyStopRound
        {
            get => _earlyStopRound;
            set
            {
                SetProperty(ref _earlyStopRound, value);
                OnPropertyChanged(nameof(EarlyStopRound));
                ShowNextBtn();
            }
        }
        /// <summary>
        /// 是否可显示下一步按钮
        /// </summary>
        [ObservableProperty]
        private bool isVisibleNextStep = false;
        #endregion

        #region 命令
        [RelayCommand]
        private async Task GoToNextTab(UserControl o)
        {
            if (o != null && o.Parent != null)
            {
                if (o.Parent.Parent is TabControl control)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        control.SelectedIndex = 3;
                    });
                }
            }
        }
        [RelayCommand]
        private void SaveConfig()
        {
            App.TrainModel.LearningRate = SelectedLearningRate;
            App.TrainModel.BatchSize = SelectedBatchSize;
            App.TrainModel.Optimizer = SelectedOptimizer;
            App.TrainModel.UseDataAugmentation = false;
            App.TrainModel.Epochs = Epochs;
            //TrainModel trainModel = new()
            //{
            //    BatchSize = SelectedBatchSize,
            //    DataDir = "D:\\Sources\\ModelTraining",
            //    Epochs = Epochs,
            //    LearningRate = SelectedLearningRate,
            //    ModelName = "resnet-18",
            //    ModelSavePath = "D:\\Sources\\ModelTraining\\model.pth",
            //    NumClasses = 5,
            //    Optimizer = "adam",
            //    StatusFile = "D:\\Sources\\ModelTraining\\statusFile.json",
            //    UseDataAugmentation = true,
            //};
            string jsonStr = JsonConvert.SerializeObject(App.TrainModel);
            string configPath = Path.Combine(App.ConfigFolderPath, "ModelParam.json");
            File.WriteAllTextAsync(jsonStr, configPath);
        }
        #endregion

        #region 函数
        private void ShowNextBtn()
        {
            IsVisibleNextStep = (Epochs > 0 && EarlyStopRound > 0) ? true : false;
        }
        #endregion
    }
}
