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
using System.Linq;

namespace AutoTrainer;

public partial class DatasetAnnotationView : UserControl
{

    private DatasetAnnotationViewModel _viewModel;
    public DatasetAnnotationView()
    {
        InitializeComponent();
        _viewModel = new DatasetAnnotationViewModel();
        DataContext = _viewModel;

        AnnotationCanvas.PointerPressed += OnCanvasPointerPressed;
        AnnotationCanvas.PointerMoved += OnCanvasPointerMoved;
        AnnotationCanvas.PointerReleased += OnCanvasPointerReleased;
        AnnotationCanvas.KeyDown += OnCanvasKeyDown;

        // 确保Canvas可以接收键盘焦点
        AnnotationCanvas.Focusable = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 只响应左键
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        var position = e.GetPosition(AnnotationCanvas);
        _viewModel?.StartDrawing(position);

        // 获取键盘焦点以支持ESC取消
        AnnotationCanvas.Focus();

        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel?.IsDrawing != true) return;

        var position = e.GetPosition(AnnotationCanvas);
        _viewModel.UpdateDrawing(position);

        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewModel?.IsDrawing != true) return;

        var position = e.GetPosition(AnnotationCanvas);
        _viewModel.FinishDrawing(position);

        e.Handled = true;
    }

    private void OnCanvasKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _viewModel?.CancelDrawing();
                e.Handled = true;
                break;
            case Key.Enter when _viewModel?.CurrentTool == AnnotationTool.Polygon:
                _viewModel?.FinishPolygonDrawing();
                e.Handled = true;
                break;
        }
    }

    // 处理多边形的双击完成
    private void OnCanvasDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel?.CurrentTool == AnnotationTool.Polygon && _viewModel.IsDrawing)
        {
            _viewModel.FinishPolygonDrawing();
            e.Handled = true;
        }
    }
}