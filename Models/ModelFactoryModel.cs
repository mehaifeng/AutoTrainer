using AutoTrainer.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;

namespace AutoTrainer.Models
{
    public partial class ModelFactoryModel : ViewModelBase
    {
        [ObservableProperty]
        private bool isChecked;
        [ObservableProperty]
        private string modelName = string.Empty;
        [ObservableProperty]
        private long modelSize = 0;
        [ObservableProperty]
        private string modelType = string.Empty;
        [ObservableProperty]
        private string modelVersion = string.Empty;
        [ObservableProperty]
        private DateTime lastModifiedDateTime;
        [ObservableProperty]
        private int downloadProgress;

        [RelayCommand]
        private void CheckedOne()
        {
            WeakReferenceMessenger.Default.Send(this,"CheckedSingleToken");
        }
    }
}
