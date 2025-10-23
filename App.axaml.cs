using AutoTrainer.Models;
using AutoTrainer.ViewModels;
using AutoTrainer.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AutoTrainer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            Log.Information("初始化 AutoTrainer 应用程序");
            try
            {
                AvaloniaXamlLoader.Load(this);
                Log.Debug("Avalonia XAML 加载成功");

                CheckDirectory();
                Log.Debug("目录结构检查并创建完成");

                TrainModel = new TrainModel
                {
                    ModelOutputPath = ModelOutputFolderPath,
                    PyTrainLogOutputPath = PyTrainLogsFolderPath,
                    MutationDataPath = MutationDataPath,
                };
                Log.Debug("TrainModel 初始化完成，路径 - 模型输出: {ModelOutput}, Python训练日志: {PyTrainLogs}, 变异数据: {MutationData}",
                    ModelOutputFolderPath, PyTrainLogsFolderPath, MutationDataPath);

                string osDescription = RuntimeInformation.OSDescription;
                string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
                string osPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
                    "Unknown";

                Log.Information("运行在 {OSPlatform} {Architecture} - {Description}", osPlatform, osArchitecture, osDescription);

                Log.Information("AutoTrainer 应用程序初始化成功");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "AutoTrainer 应用程序初始化失败");
                throw;
            }
        }
        /// <summary>
        /// ip，key，符号等
        /// </summary>
        public static TrainModel TrainModel { get; set; } = new TrainModel();
        public static string LineBreak = OperatingSystem.IsWindows() ? "\r\n" : OperatingSystem.IsLinux()? "\n" : "\r";
        public static string Separator = OperatingSystem.IsWindows() ? "\\" : "/";
        public static readonly string endPoint = "127.0.0.1:9000";
        public static readonly string accessKey = "Jv3FFA8htlzEpIRBcVBI";
        public static readonly string secretKey = "RGdOvEMrV9flZOAEQtN4FYPWJf2xoaXjD0zlszAs";

        #region 全局变量
        public static string PythonVenvPath { get; set; } = string.Empty;
        public static string ConfigFolderPath = Path.Combine(Environment.CurrentDirectory, "Configs");
        public static string ModelOutputFolderPath = Path.Combine(Environment.CurrentDirectory, "Models");
        public static string PyTrainLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs", "PyTrain");
        public static string PyClassifyLogFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs", "PyClassify");
        public static string AppLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs", "AppLogs");
        public static string MutationDataPath = Path.Combine(Environment.CurrentDirectory, "DataSet", "MutationDatas");
        public static string ObjDownloadPath = Path.Combine(Environment.CurrentDirectory, "Downloads");
        public static string AugmentTrainingDataPath = Path.Combine(Environment.CurrentDirectory, "DataSet", "AugmentTrainingData");

        private static void CheckDirectory()
        {
            Log.Debug("检查并创建所需目录");

            var directories = new[]
            {
                (ConfigFolderPath, "Config"),
                (ModelOutputFolderPath, "Model Output"),
                (PyTrainLogsFolderPath, "PyTrain Logs"),
                (AppLogsFolderPath, "App Logs"),
                (PyClassifyLogFolderPath, "PyClassify Logs"),
                (MutationDataPath, "Mutation Data"),
                (ObjDownloadPath, "Object Downloads"),
                (AugmentTrainingDataPath, "Augmented Training Data")
            };

            foreach (var (path, name) in directories)
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        Log.Debug("创建目录: {Name} 位置: {Path}", name, path);
                        Directory.CreateDirectory(path);
                        Log.Debug("成功创建目录: {Name}", name);
                    }
                    else
                    {
                        Log.Debug("目录已存在: {Name} 位置: {Path}", name, path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创建目录失败: {Name} 位置: {Path}", name, path);
                    throw;
                }
            }

            Log.Information("目录结构验证完成");
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