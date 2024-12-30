using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class ModelStorehouse : Window
{
    private ModelStorehouseViewModel _viewModel;
    public ModelStorehouse()
    {
        InitializeComponent();
        InitializeViewModelAsync();
    }

    private async void InitializeViewModelAsync()
    {
        _viewModel = await ModelStorehouseViewModel.CreatAsync();
        DataContext = _viewModel;
    }

    private void Window_Closed(object? sender, System.EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            Window mainWindow = desktop.MainWindow;
            mainWindow.IsVisible = true;
        }
    }
}
