using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace AutoTrainer;

public partial class ConvertToExportView : UserControl
{
    private ConvertToExportViewModel _viewmodel;
    public ConvertToExportView()
    {
        InitializeComponent();
        _viewmodel = new ConvertToExportViewModel();
        DataContext = _viewmodel;
    }
}