using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace AutoTrainer.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public event Action<string> AnyPropertyChanged;
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            AnyPropertyChanged?.Invoke(e.PropertyName);
        }
    }
}
