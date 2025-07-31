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

    private DatasetAnnotationViewModel _viewmodel;
    public DatasetAnnotationView()
    {
        InitializeComponent();
        _viewmodel = new DatasetAnnotationViewModel();
        DataContext = _viewmodel;

        AnnotationCanvas.PointerPressed += OnCanvasPointerPressed;
        AnnotationCanvas.PointerMoved += OnCanvasPointerMoved;
        AnnotationCanvas.PointerReleased += OnCanvasPointerReleased;

        // 添加键盘事件处理（用于完成多边形绘制等）
        this.KeyDown += OnViewKeyDown;
        this.Focusable = true; // 确保能接收键盘事件
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewmodel == null) return;

        // 只处理左键点击
        if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            return;
        }

        var canvas = sender as Canvas;
        var position = e.GetPosition(canvas);

        switch (_viewmodel.CurrentTool)
        {
            case AnnotationTool.Rectangle:
                StartRectangleDrawing(position);
                break;

            case AnnotationTool.Polygon:
                HandlePolygonClick(position);
                break;

            case AnnotationTool.Point:
                CreatePointAnnotation(position);
                break;
        }

        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewmodel == null) return;

        var canvas = sender as Canvas;
        var currentPosition = e.GetPosition(canvas);

        switch (_viewmodel.CurrentTool)
        {
            case AnnotationTool.Rectangle:
                UpdateRectangleDrawing(currentPosition);
                break;

            case AnnotationTool.Polygon:
                UpdatePolygonPreview(currentPosition);
                break;
        }

        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewmodel == null) return;

        switch (_viewmodel.CurrentTool)
        {
            case AnnotationTool.Rectangle:
                FinishRectangleDrawing();
                break;
                // 多边形和点标注在Released事件中不需要特殊处理
        }

        e.Handled = true;
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewmodel == null) return;

        switch (e.Key)
        {
            case Key.Escape:
                CancelCurrentDrawing();
                break;

            case Key.Enter:
            case Key.Space:
                if (_viewmodel.CurrentTool == AnnotationTool.Polygon && _viewmodel.IsDrawingPolygon)
                {
                    FinishPolygonDrawing();
                }
                break;
        }

        e.Handled = true;
    }

    // === 矩形绘制方法 ===
    private void StartRectangleDrawing(Point startPoint)
    {
        if (_viewmodel == null) return;

        _viewmodel.IsDrawing = true;
        _viewmodel.drawingStartPoint = startPoint;

        // 创建矩形标注项
        _viewmodel.CurrentDrawingItem = new AnnotationItem(
            startPoint.X, startPoint.Y, 0, 0, _viewmodel.CurrentClassName);

        // 创建矩形UI元素
        CreateRectangleElement(_viewmodel.CurrentDrawingItem);
    }

    private void UpdateRectangleDrawing(Point currentPosition)
    {
        if (!_viewmodel?.IsDrawing == true ||
            _viewmodel?.CurrentDrawingItem == null ||
            _viewmodel.drawingStartPoint == null) return;

        var startPoint = _viewmodel.drawingStartPoint.Value;

        // 计算矩形的位置和大小
        double x = Math.Min(startPoint.X, currentPosition.X);
        double y = Math.Min(startPoint.Y, currentPosition.Y);
        double width = Math.Abs(currentPosition.X - startPoint.X);
        double height = Math.Abs(currentPosition.Y - startPoint.Y);

        // 更新标注项属性
        _viewmodel.CurrentDrawingItem.X = x;
        _viewmodel.CurrentDrawingItem.Y = y;
        _viewmodel.CurrentDrawingItem.Width = width;
        _viewmodel.CurrentDrawingItem.Height = height;

        // 更新UI元素
        UpdateRectangleElement(_viewmodel.CurrentDrawingItem);
    }

    private void FinishRectangleDrawing()
    {
        if (!_viewmodel?.IsDrawing == true || _viewmodel?.CurrentDrawingItem == null) return;

        _viewmodel.IsDrawing = false;

        // 检查矩形是否有效（有一定的大小）
        if (_viewmodel.CurrentDrawingItem.Width > 5 && _viewmodel.CurrentDrawingItem.Height > 5)
        {
            // 添加到标注集合
            _viewmodel.CurrentImageAnnotations.Add(_viewmodel.CurrentDrawingItem);
        }
        else
        {
            // 如果矩形太小，删除UI元素
            RemoveAnnotationElement(_viewmodel.CurrentDrawingItem);
        }

        // 清理
        _viewmodel.CurrentDrawingItem = null;
        _viewmodel.drawingStartPoint = null;
    }

    // === 多边形绘制方法 ===
    private void HandlePolygonClick(Point clickPoint)
    {
        if (_viewmodel == null) return;

        if (!_viewmodel.IsDrawingPolygon)
        {
            // 开始新的多边形
            StartPolygonDrawing(clickPoint);
        }
        else
        {
            // 添加新的顶点
            AddPolygonPoint(clickPoint);
        }
    }

    private void StartPolygonDrawing(Point startPoint)
    {
        if (_viewmodel == null) return;

        _viewmodel.IsDrawingPolygon = true;

        // 创建多边形标注项
        var points = new List<Avalonia.Point> { startPoint };
        _viewmodel.CurrentDrawingItem = new AnnotationItem(points, _viewmodel.CurrentClassName);

        // 创建多边形UI元素
        CreatePolygonElement(_viewmodel.CurrentDrawingItem);
    }

    private void AddPolygonPoint(Point newPoint)
    {
        if (_viewmodel?.CurrentDrawingItem == null || !_viewmodel.IsDrawingPolygon) return;

        // 检查是否点击了起始点附近（闭合多边形）
        var firstPoint = _viewmodel.CurrentDrawingItem.Points.FirstOrDefault();
        var distance = Math.Sqrt(Math.Pow(newPoint.X - firstPoint.X, 2) + Math.Pow(newPoint.Y - firstPoint.Y, 2));

        if (_viewmodel.CurrentDrawingItem.Points.Count >= 3 && distance < 10) // 10像素容差
        {
            // 闭合多边形
            FinishPolygonDrawing();
            return;
        }

        // 添加新顶点
        _viewmodel.CurrentDrawingItem.Points.Add(newPoint);
        _viewmodel.CurrentDrawingItem.UpdateBoundingBox();

        // 更新UI元素
        UpdatePolygonElement(_viewmodel.CurrentDrawingItem);
    }

    private void UpdatePolygonPreview(Point currentPosition)
    {
        if (!_viewmodel?.IsDrawingPolygon == true || _viewmodel?.CurrentDrawingItem == null) return;

        // 更新多边形预览（显示从最后一个点到鼠标当前位置的线条）
        UpdatePolygonPreviewLine(_viewmodel.CurrentDrawingItem, currentPosition);
    }

    private void FinishPolygonDrawing()
    {
        if (!_viewmodel?.IsDrawingPolygon == true || _viewmodel.CurrentDrawingItem == null) return;

        _viewmodel.IsDrawingPolygon = false;

        // 检查多边形是否有效（至少3个点）
        if (_viewmodel.CurrentDrawingItem.Points.Count >= 3)
        {
            // 更新边界框
            _viewmodel.CurrentDrawingItem.UpdateBoundingBox();

            // 添加到标注集合
            _viewmodel.CurrentImageAnnotations.Add(_viewmodel.CurrentDrawingItem);

            // 移除预览线
            RemovePolygonPreviewLine(_viewmodel.CurrentDrawingItem);
        }
        else
        {
            // 如果点太少，删除UI元素
            RemoveAnnotationElement(_viewmodel.CurrentDrawingItem);
        }

        // 清理
        _viewmodel.CurrentDrawingItem = null;
    }

    /// <summary>
    /// 点标注方法
    /// </summary>
    /// <param name="clickPoint"></param>
    private void CreatePointAnnotation(Point clickPoint)
    {
        if (_viewmodel == null) return;

        // 创建点标注
        var pointAnnotation = new AnnotationItem(clickPoint.X, clickPoint.Y, _viewmodel.CurrentClassName);

        // 创建点UI元素
        CreatePointElement(pointAnnotation);

        // 直接添加到标注集合
        _viewmodel.CurrentImageAnnotations.Add(pointAnnotation);
    }

    private void CreateRectangleElement(AnnotationItem item)
    {
        var rectangle = new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(rectangle, item.X);
        Canvas.SetTop(rectangle, item.Y);
        rectangle.Width = item.Width;
        rectangle.Height = item.Height;

        AnnotationCanvas.Children.Add(rectangle);
        item.UIElement = rectangle;
    }

    private void UpdateRectangleElement(AnnotationItem item)
    {
        if (item.UIElement is Rectangle rectangle)
        {
            Canvas.SetLeft(rectangle, item.X);
            Canvas.SetTop(rectangle, item.Y);
            rectangle.Width = Math.Max(0, item.Width);
            rectangle.Height = Math.Max(0, item.Height);
        }
    }

    private void CreatePolygonElement(AnnotationItem item)
    {
        var polygon = new Polygon
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Colors.Blue) { Opacity = 0.1 },
            IsHitTestVisible = false
        };

        // 设置多边形顶点
        UpdatePolygonPoints(polygon, item.Points);

        AnnotationCanvas.Children.Add(polygon);
        item.UIElement = polygon;
    }

    private void UpdatePolygonElement(AnnotationItem item)
    {
        if (item.UIElement is Polygon polygon)
        {
            UpdatePolygonPoints(polygon, item.Points);
        }
    }

    private void UpdatePolygonPoints(Polygon polygon, ObservableCollection<Avalonia.Point> points)
    {
        polygon.Points.Clear();
        foreach (var point in points)
        {
            polygon.Points.Add(point);
        }
    }

    private void UpdatePolygonPreviewLine(AnnotationItem item, Point currentPosition)
    {
        // 这里可以添加预览线的逻辑，显示从最后一个点到当前鼠标位置的虚线
        // 为简化，这里省略具体实现
    }

    private void RemovePolygonPreviewLine(AnnotationItem item)
    {
        // 移除预览线的逻辑
    }

    private void CreatePointElement(AnnotationItem item)
    {
        var ellipse = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.Green,
            Stroke = Brushes.DarkGreen,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(ellipse, item.X - 4); // 居中
        Canvas.SetTop(ellipse, item.Y - 4);

        AnnotationCanvas.Children.Add(ellipse);
        item.UIElement = ellipse;
    }

    private void RemoveAnnotationElement(AnnotationItem item)
    {
        if (item.UIElement != null)
        {
            AnnotationCanvas.Children.Remove(item.UIElement);
            item.UIElement = null;
        }
    }

    private void CancelCurrentDrawing()
    {
        if (_viewmodel == null) return;

        if (_viewmodel.IsDrawing && _viewmodel.CurrentDrawingItem != null)
        {
            // 取消矩形绘制
            RemoveAnnotationElement(_viewmodel.CurrentDrawingItem);
            _viewmodel.CurrentDrawingItem = null;
            _viewmodel.IsDrawing = false;
            _viewmodel.drawingStartPoint = null;
        }

        if (_viewmodel.IsDrawingPolygon && _viewmodel.CurrentDrawingItem != null)
        {
            // 取消多边形绘制
            RemoveAnnotationElement(_viewmodel.CurrentDrawingItem);
            _viewmodel.CurrentDrawingItem = null;
            _viewmodel.IsDrawingPolygon = false;
        }
    }
}