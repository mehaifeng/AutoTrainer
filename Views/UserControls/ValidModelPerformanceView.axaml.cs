using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class ValidModelPerformanceView : UserControl
{
    private readonly ValidModelPerformanceViewModel _viewModel;
    public ValidModelPerformanceView()
    {
        _viewModel = new ValidModelPerformanceViewModel();
        DataContext = _viewModel;
        InitializeComponent();
    }
}