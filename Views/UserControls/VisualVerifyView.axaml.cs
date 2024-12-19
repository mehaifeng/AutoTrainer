using AutoTrainer.Models;
using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AutoTrainer;

public partial class VisualVerifyView : UserControl
{

    private VisualVerifyViewModel _viewmodel;
    public VisualVerifyView()
    {
        InitializeComponent();
        _viewmodel = new VisualVerifyViewModel();
        DataContext = _viewmodel;
    }

    private void classifiedImage_btn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is Image image && image.DataContext is Thumbnail imageInfo)
        {
            _viewmodel.SelectImage = imageInfo;
            _viewmodel.PredictClass = imageInfo.PredictClass ?? "";
            _viewmodel.Confidence = imageInfo.Confidence.ToString();
            _viewmodel.ActualClass = imageInfo.ActualClass ?? "";
            _viewmodel.ImagePath = imageInfo.ImagePath ?? "";
        }
    }
}