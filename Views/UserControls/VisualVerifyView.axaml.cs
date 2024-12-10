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
        if (sender is Button)
        {
            var btn = (Button)sender;
            var image = btn.Content as Image;
            if (image != null)
            {
                Thumbnail imageInfo = (Thumbnail)image.DataContext;
                _viewmodel.SelectImage = imageInfo;
                _viewmodel.DescribeImage = $"识别类别:\n{imageInfo.PredictClass}\n置信度:\n{imageInfo.Confidence}\n实际类别:\n{imageInfo.ActualClass}\n图片路径:\n{imageInfo.ImagePath}";
            }
        }
    }
}