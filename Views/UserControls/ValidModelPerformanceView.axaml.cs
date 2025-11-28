using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace AutoTrainer.Views.UserControls;

public partial class ValidModelPerformanceView : UserControl
{
    private readonly ValidModelPerformanceViewModel _viewModel;
    public ValidModelPerformanceView()
    {
        _viewModel = new ValidModelPerformanceViewModel();
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void OnPathTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is string path)
        {
            _viewModel.OpenImageLocationCommand.Execute(path);
        }
    }

    private void OnImageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is Bitmap image)
        {
            try
            {
                var viewer = new ImageViewerWindow();
                viewer.SetImage(image);
                
                // 获取当前UserControl所在的顶层窗口
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is Window ownerWindow && ownerWindow.IsVisible)
                {
                    viewer.ShowDialog(ownerWindow);
                }
                else
                {
                    viewer.Show();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "无法显示图片查看器");
            }
        }
    }
    
    private void OnValidationPreviewImageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is string imagePath)
        {
            try
            {
                // 从原图路径加载完整尺寸的图片
                if (File.Exists(imagePath))
                {
                    var originalBitmap = new Bitmap(imagePath);
                    
                    var viewer = new ImageViewerWindow();
                    viewer.SetImage(originalBitmap);
                    
                    // 获取当前UserControl所在的顶层窗口
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel is Window ownerWindow && ownerWindow.IsVisible)
                    {
                        viewer.ShowDialog(ownerWindow);
                    }
                    else
                    {
                        viewer.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "无法显示验证集预览图片");
            }
        }
    }
}