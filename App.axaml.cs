using AutoTrainer.Models;
using AutoTrainer.ViewModels;
using AutoTrainer.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AutoTrainer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            CheckDirectory();
            TrainModel = new TrainModel
            {
                ModelOutputPath = ModelOutputFolderPath,
                PyTrainLogOutputPath = PyTrainLogsFolderPath,
                MutationDataPath = MutationDataPath,
            };
            string osDescription = RuntimeInformation.OSDescription; // 获取操作系统描述
            string osArchitecture = RuntimeInformation.OSArchitecture.ToString(); // 获取操作系统架构
            string osPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
                "Unknown";
        }
        /// <summary>
        /// һ��ȫ�ֲ���
        /// </summary>
        public static TrainModel TrainModel { get; set; } = new TrainModel();
        public static string LineBreak = OperatingSystem.IsWindows() ? "\r\n" : OperatingSystem.IsLinux()? "\n" : "\r";
        public static string Separator = OperatingSystem.IsWindows() ? "\\" : "/";
        public static readonly string endPoint = "127.0.0.1:9000";
        public static readonly string accessKey = "Jv3FFA8htlzEpIRBcVBI";
        public static readonly string secretKey = "RGdOvEMrV9flZOAEQtN4FYPWJf2xoaXjD0zlszAs";
        #region ����·�������ʼ��
        public static string PythonVenvPath { get; set; } = string.Empty;
        public static string ConfigFolderPath = Path.Combine(Environment.CurrentDirectory, "Configs");
        public static string ModelOutputFolderPath = Path.Combine(Environment.CurrentDirectory, "Models");
        public static string PyTrainLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs","PyTrain");
        public static string PyClassifyLogFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs","PyClassify");
        public static string AppLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs","AppLogs");
        public static string MutationDataPath = Path.Combine(Environment.CurrentDirectory, "DataSet","MutationDatas");
        public static string ObjDownloadPath = Path.Combine(Environment.CurrentDirectory, "Downloads");
        public static string AugmentTrainingDataPath = Path.Combine(Environment.CurrentDirectory, "DataSet", "AugmentTrainingData");
        private static void CheckDirectory()
        {
            if (!Directory.Exists(ConfigFolderPath)) Directory.CreateDirectory(ConfigFolderPath);
            if (!Directory.Exists(ModelOutputFolderPath)) Directory.CreateDirectory(ModelOutputFolderPath);
            if (!Directory.Exists(PyTrainLogsFolderPath)) Directory.CreateDirectory(PyTrainLogsFolderPath);
            if (!Directory.Exists(AppLogsFolderPath)) Directory.CreateDirectory(AppLogsFolderPath);
            if (!Directory.Exists(PyClassifyLogFolderPath)) Directory.CreateDirectory(PyClassifyLogFolderPath);
            if (!Directory.Exists(MutationDataPath)) Directory.CreateDirectory(MutationDataPath);
            if (!Directory.Exists(ObjDownloadPath)) Directory.CreateDirectory(ObjDownloadPath);
            if (!Directory.Exists(AugmentTrainingDataPath)) Directory.CreateDirectory(AugmentTrainingDataPath);
        }
        #endregion

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Line below is needed to remove Avalonia data validation.
                // Without this line you will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);
                desktop.MainWindow = new SelectTrainingTypeView();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}