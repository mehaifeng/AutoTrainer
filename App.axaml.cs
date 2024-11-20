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
            TrainModel = new TrainModel();
        }
        public static TrainModel TrainModel { get; set; }
        public static string ConfigFolderPath = Path.Combine(Environment.CurrentDirectory, "Configs");
        private void CheckDirectory()
        {
            if (!Directory.Exists(ConfigFolderPath))
            {
                Directory.CreateDirectory(ConfigFolderPath);
            }
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