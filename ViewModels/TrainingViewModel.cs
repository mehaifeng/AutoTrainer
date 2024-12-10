using AutoTrainer.Helpers;
using AutoTrainer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class TrainingViewModel : ViewModelBase
    {
        private bool isPyRunning = false;
        private readonly CancellationTokenSource cancellationTokenSource;
        private int scanningIndex = 0;
        public TrainingViewModel()
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
            cancellationTokenSource = new CancellationTokenSource();
        }

        #region 可绑定属性

        [ObservableProperty]
        private ObservableCollection<ISeries> series;
        /// <summary>
        /// 训练准确度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue> trainAccValues;
        /// <summary>
        /// 训练损失度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue> trainLossValues;
        /// <summary>
        /// 验证准确度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue> validationAccValues;
        /// <summary>
        /// 验证损失度
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ObservableValue> validationLossValues;
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
            var sb = new StringBuilder();
            var modelParam = App.TrainModel;
            sb.AppendLine("模型: " + modelParam.PretrainedModel);
            sb.AppendLine("学习率: " + modelParam.LearningRate);
            sb.AppendLine("优化器: " + modelParam.Optimizer);
            sb.AppendLine("学习率调度器: " + modelParam.LrScheduler);
            sb.AppendLine("权重衰减: " + modelParam.WeightDecay);
            sb.AppendLine("批量大小: " + modelParam.BatchSize);
            sb.AppendLine("训练轮数: " + modelParam.Epochs);
            sb.AppendLine("早停轮数: " + modelParam.EarlyStoppingRounds);
            sb.AppendLine("验证集比例: " + modelParam.ValidationSplit);
            sb.AppendLine("使用数据增强: " + modelParam.UseDataAugmentation);
            sb.AppendLine("训练数据路径: " + modelParam.TrainDataPath);
            sb.AppendLine("验证数据路径: " + modelParam.ValDataPath);
            sb.AppendLine("模型保存路径: " + modelParam.ModelOutputPath);
            sb.AppendLine("训练日志输出路径: " + modelParam.PyTrainLogOutputPath);
            EpochState.TotalEpochs = modelParam.Epochs;
            ModelParamStr = sb.ToString();
        }
        /// <summary>
        /// 开始训练
        /// </summary>
        [RelayCommand]
        private async Task StartTraining()
        {
            var currentPyLogfile = Path.Combine(App.PyTrainLogsFolderPath, "Log" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            App.TrainModel.PyTrainLogOutputPath = currentPyLogfile;
            var jsonStr = JsonConvert.SerializeObject(App.TrainModel);
            await File.WriteAllTextAsync(Path.Combine(App.ConfigFolderPath, "ModelParam.json"), jsonStr);
            var sb = new StringBuilder();
            sb.Append($"{App.PythonVenvPath}\\Scripts\\activate.bat");
            sb.Append(" && ");
            sb.Append($"python {Environment.CurrentDirectory}\\PyScripts\\Train.py --config {Environment.CurrentDirectory}\\Configs\\ModelParam.json");
            _ = Task.Run(() => ScanningThePyOutPut(cancellationTokenSource.Token));
            isPyRunning = true;
            await CmdHelper.ExecuteCmdWindow(sb.ToString(), false);
            isPyRunning = false;
            await cancellationTokenSource.CancelAsync();
            await ReadPyOutputAtMeantime();
        }
        #endregion

        #region 函数

        /// <summary>
        /// Py文件执行时，处理各种任务
        /// </summary>
        /// <returns></returns>
        private async Task ScanningThePyOutPut(CancellationToken token)
        {
            //DateTime currentLoadTime;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!File.Exists(App.TrainModel.PyTrainLogOutputPath))
                    {
                        await Task.Delay(3000, CancellationToken.None);
                        continue;
                    }
                    //
                    await ReadPyOutputAtMeantime();
                    await Task.Delay(3000, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>
        /// 读取Py脚本输出的json，绘图，输出和控制进度条
        /// </summary>
        private async Task ReadPyOutputAtMeantime()
        {
            var jsonStr = await File.ReadAllTextAsync(App.TrainModel.PyTrainLogOutputPath);
            var pyExecuteOutput = JsonConvert.DeserializeObject<TrainingLog>(jsonStr);
            if (pyExecuteOutput is { Entries.Count: > 0 })
            {
                if (scanningIndex >= pyExecuteOutput.Entries.Count)
                {
                    return;
                }
                for (var i = scanningIndex; i < pyExecuteOutput.Entries.Count; i++)
                {
                    //画图方面，需要找到type为Validation的消息
                    if (string.Equals(pyExecuteOutput.Entries[i].Type, "Validation"))
                    {
                        TrainAccValues.Add(new ObservableValue()
                        { Value = pyExecuteOutput.Entries[i].Metrics.TrainAccuracy });
                        TrainLossValues.Add(new ObservableValue()
                        { Value = pyExecuteOutput.Entries[i].Metrics.TrainLoss });
                        ValidationAccValues.Add(new ObservableValue()
                        { Value = pyExecuteOutput.Entries[i].Metrics.ValidationAccuracy });
                        ValidationLossValues.Add(new ObservableValue()
                        { Value = pyExecuteOutput.Entries[i].Metrics.ValidationLoss });
                        EpochState.CurrentEpoch = pyExecuteOutput.Entries[i].Epoch;
                    }
                    //打印输出信息
                    PyOutput += pyExecuteOutput.Entries[i].Message + "\r\n";
                    scanningIndex++;
                }
            }

            if (!isPyRunning && EpochState.CurrentEpoch < EpochState.TotalEpochs)
            {
                EpochState.TotalEpochs = EpochState.CurrentEpoch;
            }
        }
        #endregion
    }
}
