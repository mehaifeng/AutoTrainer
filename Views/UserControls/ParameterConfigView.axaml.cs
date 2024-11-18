using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class ParameterConfigView : UserControl
{
    private ParameterConfigViewModel _viewmodel;
    public ParameterConfigView()
    {
        InitializeComponent();
        _viewmodel = new ParameterConfigViewModel();
        DataContext = _viewmodel;
    }
}