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
using Avalonia.Media.Imaging;

namespace AutoTrainer;

public partial class DatasetAnnotationView : UserControl
{

    private DatasetAnnotationViewModel _viewmodel;
    public DatasetAnnotationView()
    {
        InitializeComponent();
        _viewmodel = new DatasetAnnotationViewModel();
        DataContext = _viewmodel;
    }
    //private bool _isDragging;
    //private Point _lastPointerPosition;
    //private double _zoomFactor = 1.0;
    //private const double MinZoom = 0.1;
    //private const double MaxZoom = 10.0;
    //private const double ZoomStep = 0.1;


    //// 设置图像源
    //public void SetImageSource(Bitmap bitmap)
    //{
    //    MainImage.Source = bitmap;
    //    _zoomFactor = 1.0;
    //    UpdateImageTransform();
    //}

    //private void Image_PointerPressed(object? sender, PointerPressedEventArgs e)
    //{
    //    if (e.GetCurrentPoint(MainImage).Properties.IsLeftButtonPressed)
    //    {
    //        _isDragging = true;
    //        _lastPointerPosition = e.GetPosition(ImageScrollViewer);
    //        MainImage.Cursor = new Cursor(StandardCursorType.Hand);
    //        e.Handled = true;
    //    }
    //}

    //private void Image_PointerMoved(object? sender, PointerEventArgs e)
    //{
    //    if (_isDragging)
    //    {
    //        var currentPosition = e.GetPosition(ImageScrollViewer);
    //        var deltaX = currentPosition.X - _lastPointerPosition.X;
    //        var deltaY = currentPosition.Y - _lastPointerPosition.Y;

    //        // 更新ScrollViewer的偏移量
    //        ImageScrollViewer.Offset = new Vector(
    //            ImageScrollViewer.Offset.X - deltaX,
    //            ImageScrollViewer.Offset.Y - deltaY);

    //        _lastPointerPosition = currentPosition;
    //        e.Handled = true;
    //    }
    //}

    //private void Image_PointerReleased(object? sender, PointerReleasedEventArgs e)
    //{
    //    if (_isDragging)
    //    {
    //        _isDragging = false;
    //        MainImage.Cursor = new Cursor(StandardCursorType.Arrow);
    //        e.Handled = true;
    //    }
    //}

    //private void Image_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    //{
    //    var delta = e.Delta.Y;
    //    var newZoom = _zoomFactor + (delta > 0 ? ZoomStep : -ZoomStep);

    //    // 限制缩放范围
    //    newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

    //    if (Math.Abs(newZoom - _zoomFactor) > 0.001)
    //    {
    //        _zoomFactor = newZoom;
    //        UpdateImageTransform();
    //    }

    //    e.Handled = true;
    //}

    //private void UpdateImageTransform()
    //{
    //    var transform = new ScaleTransform(_zoomFactor, _zoomFactor);
    //    MainImage.RenderTransform = transform;
    //}

    //// 重置缩放
    //public void ResetZoom()
    //{
    //    _zoomFactor = 1.0;
    //    UpdateImageTransform();
    //    ImageScrollViewer.Offset = Vector.Zero;
    //}

    //// 适应窗口大小
    //public void FitToWindow()
    //{
    //    if (MainImage.Source is Bitmap bitmap)
    //    {
    //        var containerWidth = ImageScrollViewer.Bounds.Width;
    //        var containerHeight = ImageScrollViewer.Bounds.Height;

    //        var scaleX = containerWidth / bitmap.PixelSize.Width;
    //        var scaleY = containerHeight / bitmap.PixelSize.Height;

    //        _zoomFactor = Math.Min(scaleX, scaleY);
    //        UpdateImageTransform();
    //        ImageScrollViewer.Offset = Vector.Zero;
    //    }
    //}
}