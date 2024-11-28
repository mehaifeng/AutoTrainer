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
                LogOutputPath = PyLogsFolderPath,
                MutationDataPath = MutationDataPath,
            };
        }
        public static TrainModel TrainModel { get; set; }
        public static string PythonVenvPath { get; set; }
        public static string ConfigFolderPath = Path.Combine(Environment.CurrentDirectory, "Configs");
        public static string ModelOutputFolderPath = Path.Combine(Environment.CurrentDirectory, "Models");
        public static string PyLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs\\PyLogs");
        public static string AppLogsFolderPath = Path.Combine(Environment.CurrentDirectory, "Logs\\AppLogs");
        public static string MutationDataPath = Path.Combine(Environment.CurrentDirectory, "DataSet\\MutationDatas");
        private static void CheckDirectory()
        {
            if (!Directory.Exists(ConfigFolderPath)) Directory.CreateDirectory(ConfigFolderPath);
            if (!Directory.Exists(ModelOutputFolderPath)) Directory.CreateDirectory(ModelOutputFolderPath);
            if (!Directory.Exists(PyLogsFolderPath)) Directory.CreateDirectory(PyLogsFolderPath);
            if (!Directory.Exists(AppLogsFolderPath)) Directory.CreateDirectory(AppLogsFolderPath);
            if (Directory.Exists(MutationDataPath)) Directory.CreateDirectory(MutationDataPath);
        }

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