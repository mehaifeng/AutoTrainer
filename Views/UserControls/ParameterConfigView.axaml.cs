using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace AutoTrainer;

public partial class ParameterConfigView : UserControl
{
    private ParameterConfigViewModel _viewmodel;
    public ParameterConfigView()
    {
        _viewmodel = new ParameterConfigViewModel();
        DataContext = _viewmodel;
        InitializeComponent();
    }
}