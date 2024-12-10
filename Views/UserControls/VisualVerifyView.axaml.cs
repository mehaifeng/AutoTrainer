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
                _viewmodel.DescribeImage = $"ʶ�����:\n{imageInfo.PredictClass}\n���Ŷ�:\n{imageInfo.Confidence}\nʵ�����:\n{imageInfo.ActualClass}\nͼƬ·��:\n{imageInfo.ImagePath}";
            }
        }
    }
}