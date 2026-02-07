using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace AutoTrainer;

public partial class ModelExportView : UserControl
{
    private ModelExportViewModel _viewmodel;
    public ModelExportView()
    {
        InitializeComponent();
        _viewmodel = new ModelExportViewModel();
        DataContext = _viewmodel;
    }
}