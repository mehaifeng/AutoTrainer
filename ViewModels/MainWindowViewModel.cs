using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoTrainer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        MainWindowViewModel()
        {

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
