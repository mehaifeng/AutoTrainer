using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System;

namespace AutoTrainer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            Log.Information("MainWindowViewModel 初始化完成");
            try
            {
                Log.Debug("MainWindowViewModel 初始化成功完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainWindowViewModel 初始化失败");
                throw;
            }
        }
        [ObservableProperty]
        private int selectTabIndex = 0;
        [ObservableProperty]
        private bool isEnableModelTab = true;
        [ObservableProperty]
        private bool isEnableParaTab = false;
        [ObservableProperty]
        private bool isEnableTrainTab = false;
        [ObservableProperty]
        private bool isEnableValidTab = false;
        [ObservableProperty]
        private bool isEnableExportTab = false;
    }
}
