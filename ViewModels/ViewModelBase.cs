using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace AutoTrainer.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public event Action<string>? AnyPropertyChanged;
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName != null)
            {
                AnyPropertyChanged?.Invoke(e.PropertyName);
            }
        }
        public static Window MainWindow 
        {
#pragma warning disable CS8603 // 可能返回 null 引用。
#pragma warning disable CS8602 // 解引用可能出现空引用。
            get => (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow;
#pragma warning restore CS8602 // 解引用可能出现空引用。
#pragma warning restore CS8603 // 可能返回 null 引用。
        }
    }
}
