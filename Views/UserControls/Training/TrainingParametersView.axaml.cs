using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace AutoTrainer;

public partial class TrainingParametersView : UserControl
{
    private TrainingParametersViewModel _viewmodel;
    public TrainingParametersView()
    {
        _viewmodel = new TrainingParametersViewModel();
        DataContext = _viewmodel;
        InitializeComponent();
    }
}