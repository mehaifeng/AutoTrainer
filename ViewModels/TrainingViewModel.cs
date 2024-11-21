using AutoTrainer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTrainer.Helpers;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace AutoTrainer.ViewModels
{
    public partial class TrainingViewModel : ViewModelBase
    {
        public TrainingViewModel()
        {

        }

        #region 可绑定属性
        [ObservableProperty]
        private ISeries[] series = [
            new ColumnSeries<int>(3, 4, 2),
        ];
        [ObservableProperty]
        private string modeleParamInfo;
        [ObservableProperty]
        private string pyOutput;
        [ObservableProperty]
        private EpochState epochState = new() { currentEpoch = 50, totallRpochs = 100 };
        #endregion

        #region 命令

        [RelayCommand]
        private void LoadParamInfo()
        {
            var sb = new StringBuilder();
            var modelParam = App.TrainModel;
            sb.AppendLine("模型: " + modelParam.PretrainedModel);
            //sb.AppendLine("类别数目: " + modelParam);
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
            sb.AppendLine("日志输出路径: " + modelParam.LogOutputPath);
            ModeleParamInfo = sb.ToString();
        }
        /// <summary>
        /// 开始训练
        /// </summary>
        [RelayCommand]
        private async void StartTraining()
        {
            var currentPyLogfile = Path.Combine(App.PyLogsFolderPath, "Log"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json");
            App.TrainModel.LogOutputPath = currentPyLogfile;
            var jsonStr = JsonConvert.SerializeObject(App.TrainModel);
            await File.WriteAllTextAsync(Path.Combine(App.ConfigFolderPath,"ModelParam.json"), jsonStr);

            var sb = new StringBuilder();
            sb.Append($"{App.PythonVenvPath}\\Scripts\\activate.bat");
            sb.Append(" && ");
            sb.Append($"python {Environment.CurrentDirectory}\\PyScripts\\Train.py --config {Environment.CurrentDirectory}\\Configs\\ModelParam.json");
            _ = Task.Run(() => ScanningThePyOutPut());
            await CmdHelper.ExecuteCmdWindow(sb.ToString());
        }
        #endregion

        #region 函数

        /// <summary>
        /// 扫描Py脚本执行输出文件
        /// </summary>
        /// <returns></returns>
        private async void ScanningThePyOutPut()
        {
            //DateTime currentLoadTime;
            while (true)
            {
                if (!File.Exists(App.TrainModel.LogOutputPath))
                {
                    await Task.Delay(3000);
                    continue;
                }
                var jsonStr = File.ReadAllText(App.TrainModel.LogOutputPath);
                var pyExcuteOutput = JsonConvert.DeserializeObject<TrainingLog>(jsonStr);
                if (pyExcuteOutput != null)
                {
                    if (pyExcuteOutput.Entries.Count > 0)
                    {

                    }
                }
                await Task.Delay(3000);
            }
        }
        

        #endregion
    }
}
