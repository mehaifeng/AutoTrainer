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
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AutoTrainer;

public partial class DatasetAnnotationView : UserControl
{

    private DatasetAnnotationViewModel _viewmodel;
    // 拖动相关字段
    private bool _isDragging = false;
    private AnnotationItem? _draggingItem = null;
    private Point _dragStartPoint;
    private Point _elementStartPosition;
    private List<Point>? _originalPolygonPoints;
    public DatasetAnnotationView()
    {
        InitializeComponent();
        _viewmodel = new DatasetAnnotationViewModel(AnnotationCanvas);
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
        //没有加载图片时，不响应标注绘制
        if (_viewmodel.CurrentImage == null)
        {
            return;
        }
        //不阻止滚轮中键点击，滚轮中键用于拖动图像
        else if (e.GetCurrentPoint(sender as Visual).Properties.IsMiddleButtonPressed)
        {
            return;
        }
        // 只处理左键点击
        else if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            return;
        }
        var canvas = sender as Canvas;
        var position = e.GetPosition(canvas);

        // 首先检查是否点击了已存在的标注元素（用于拖动）
        var hitElement = GetHitAnnotationElement(position);
        if (hitElement != null)
        {
            StartDragging(hitElement, position);
            _viewmodel?.OnAnnotationSelected?.Invoke(hitElement);
            e.Handled = true;
            return;
        }

        switch (_viewmodel.CurrentTool)
        {
            case AnnotationToolEnum.Rectangle:
                StartRectangleDrawing(position);
                break;

            case AnnotationToolEnum.Polygon:
                HandlePolygonClick(position);
                break;

            case AnnotationToolEnum.Point:
                CreatePointAnnotation(position);
                break;
        }

        //e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewmodel == null) return;

        var canvas = sender as Canvas;
        var currentPosition = e.GetPosition(canvas);

        // 如果正在拖动标注元素
        if (_isDragging && _draggingItem != null)
        {
            UpdateDragging(currentPosition);
            return;
        }

        //如果没有正在拖动的标注元素，则处理绘制逻辑
        switch (_viewmodel.CurrentTool)
        {
            case AnnotationToolEnum.Rectangle:
                UpdateRectangleDrawing(currentPosition);
                break;

            case AnnotationToolEnum.Polygon:
                UpdatePolygonPreview(currentPosition);
                break;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewmodel == null) return;

        // 如果正在拖动，结束拖动
        if (_isDragging)
        {
            FinishDragging();
            return;
        }
        // 如果没有正在拖动的标注元素，则处理绘制完成逻辑
        switch (_viewmodel.CurrentTool)
        {
            case AnnotationToolEnum.Rectangle:
                FinishRectangleDrawing();
                break;
                // 多边形和点标注在Released事件中不需要特殊处理
        }
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
                if (_viewmodel.CurrentTool == AnnotationToolEnum.Polygon && _viewmodel.IsDrawingPolygon)
                {
                    FinishPolygonDrawing();
                    CancelCurrentDrawing();
                }
                break;
        }
    }

    /// <summary>
    /// 矩形绘制方法
    /// </summary>
    /// <param name="startPoint"></param>
    private void StartRectangleDrawing(Point startPoint)
    {
        if (_viewmodel == null) return;

        _viewmodel.IsDrawing = true;
        _viewmodel.drawingStartPoint = startPoint;

        // 创建矩形标注项
        _viewmodel.CurrentDrawingItem = new RectangleModel(startPoint.X, startPoint.Y, 0, 0)
        {
            AnnotationType = AnnotationToolEnum.Rectangle,
            ClassName = _viewmodel.CurrentClassName
        };

        // 创建矩形UI元素
        CreateRectangleElement((RectangleModel)_viewmodel.CurrentDrawingItem);
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

        var rect = (RectangleModel)_viewmodel.CurrentDrawingItem;
        // 更新标注项属性
        rect.X = x;
        rect.Y = y;
        rect.Width = width;
        rect.Height = height;

        // 更新UI元素
        UpdateRectangleElement(rect);
    }

    private void FinishRectangleDrawing()
    {
        if (!_viewmodel?.IsDrawing == true || _viewmodel?.CurrentDrawingItem == null) return;

        _viewmodel.IsDrawing = false;

        var rect = (RectangleModel)_viewmodel.CurrentDrawingItem;
        // 检查矩形是否有效（有一定的大小）
        if (rect.Width > 5 && rect.Height > 5)
        {
            // 添加到标注集合
            _viewmodel.CurrentImageAnnotations.Add(_viewmodel.CurrentDrawingItem);
            if (_viewmodel.AllImageAnnotations.ContainsKey(_viewmodel.CurrentImageFileName))
            {
                _viewmodel.AllImageAnnotations[_viewmodel.CurrentImageFileName] = [.._viewmodel.CurrentImageAnnotations];
            }
            else
            {
                _viewmodel.AllImageAnnotations.Add(_viewmodel.CurrentImageFileName, [.._viewmodel.CurrentImageAnnotations]);
            }
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

    /// <summary>
    /// 多边形绘制方法
    /// </summary>
    /// <param name="clickPoint"></param>
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
        var points = new List<Point> { startPoint };
        _viewmodel.CurrentDrawingItem = new PolygonModel
        {
            Points = points,
            AnnotationType = AnnotationToolEnum.Polygon,
            ClassName = _viewmodel.CurrentClassName
        };
        var polygon = (PolygonModel)_viewmodel.CurrentDrawingItem;
        // 创建多边形UI元素
        CreatePolygonElement(polygon);
    }

    private void AddPolygonPoint(Point newPoint)
    {
        if (_viewmodel?.CurrentDrawingItem == null || !_viewmodel.IsDrawingPolygon) return;
        var polygon = (PolygonModel)_viewmodel.CurrentDrawingItem;
        // 检查是否点击了起始点附近（闭合多边形）
        var firstPoint = polygon.Points.FirstOrDefault();
        var distance = Math.Sqrt(Math.Pow(newPoint.X - firstPoint.X, 2) + Math.Pow(newPoint.Y - firstPoint.Y, 2));

        if (polygon.Points.Count >= 3 && distance < 5) // 5像素容差
        {
            // 闭合多边形
            FinishPolygonDrawing();
            return;
        }

        // 添加新顶点
        ((PolygonModel)_viewmodel.CurrentDrawingItem).Points.Add(newPoint);
        //((PolygonModel)_viewmodel.CurrentDrawingItem).GetBoundingBox();

        // 更新UI元素
        UpdatePolygonElement(_viewmodel.CurrentDrawingItem);
    }

    private void UpdatePolygonPreview(Point currentPosition)
    {
        if (!_viewmodel?.IsDrawingPolygon == true || _viewmodel?.CurrentDrawingItem == null) return;

        // 更新多边形预览
        UpdatePolygonPreviewLine(_viewmodel.CurrentDrawingItem, currentPosition);
    }

    private void FinishPolygonDrawing()
    {
        if (!_viewmodel?.IsDrawingPolygon == true || _viewmodel?.CurrentDrawingItem == null) return;
        var polygon = (PolygonModel)_viewmodel.CurrentDrawingItem;
        _viewmodel.IsDrawingPolygon = false;

        // 检查多边形是否有效（至少3个点）
        if (polygon.Points.Count >= 3)
        {
            // 更新边界框
            //polygon.GetBoundingBox();

            // 添加到标注集合
            _viewmodel.CurrentImageAnnotations.Add(_viewmodel.CurrentDrawingItem);
            if (_viewmodel.AllImageAnnotations.ContainsKey(_viewmodel.CurrentImageFileName))
            {
                _viewmodel.AllImageAnnotations[_viewmodel.CurrentImageFileName] = [.._viewmodel.CurrentImageAnnotations];
            }
            else
            {
                _viewmodel.AllImageAnnotations.Add(_viewmodel.CurrentImageFileName, [.._viewmodel.CurrentImageAnnotations]);
            }

            // 移除预览线
            RemovePolygonPreviewLine(_viewmodel.CurrentDrawingItem);
        }
        else
        {
            // 如果点太少，删除UI元素
            RemoveAnnotationElement(_viewmodel.CurrentDrawingItem);
            // 删除正在绘制的虚线
            RemovePolygonPreviewLine(_viewmodel.CurrentDrawingItem);
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
        var pointAnnotation = new PointModel(clickPoint.X, clickPoint.Y)
        {
            AnnotationType = AnnotationToolEnum.Point,
            ClassName = _viewmodel.CurrentClassName
        };
        // 创建点UI元素
        CreatePointElement(pointAnnotation);
        // 直接添加到标注集合
        _viewmodel.CurrentImageAnnotations.Add(pointAnnotation);
        if (_viewmodel.AllImageAnnotations.ContainsKey(_viewmodel.CurrentImageFileName))
        {
            _viewmodel.AllImageAnnotations[_viewmodel.CurrentImageFileName] = [.._viewmodel.CurrentImageAnnotations];
        }
        else
        {
            _viewmodel.AllImageAnnotations.Add(_viewmodel.CurrentImageFileName, [.._viewmodel.CurrentImageAnnotations]);
        }
    }
    /// <summary>
    /// 创建矩形标注元素
    /// </summary>
    /// <param name="item"></param>

    private void CreateRectangleElement(RectangleModel item)
    {
        var rectangle = new Rectangle
        {
            Tag = item.InstanceGuid,
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

    private void UpdatePointElement(AnnotationItem item)
    {
        if (item != null)
        {
            var point = (PointModel)item;
            if (point.UIElement is Ellipse ellipse)
            {
                Canvas.SetLeft(ellipse, point.X - 4); // 居中
                Canvas.SetTop(ellipse, point.Y - 4);
                point.X = Canvas.GetLeft(ellipse);
                point.Y = Canvas.GetTop(ellipse);
            }
        }
    }

    private void UpdateRectangleElement(AnnotationItem item)
    {
        if (item != null)
        {
            var rect = (RectangleModel)item;
            if (rect.UIElement is Rectangle rectangle)
            {
                Canvas.SetLeft(rectangle, rect.X);
                Canvas.SetTop(rectangle, rect.Y);
                rectangle.Width = Math.Max(0, rect.Width);
                rectangle.Height = Math.Max(0, rect.Height);
            }
        }
    }
    /// <summary>
    /// 创建多边形标注
    /// </summary>
    /// <param name="item"></param>
    private void CreatePolygonElement(PolygonModel item)
    {
        var polygon = new Polygon
        {
            Tag = item.InstanceGuid,
            Stroke = Brushes.Blue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Colors.Blue) { Opacity = 0.1 },
            IsHitTestVisible = false,
        };

        // 设置多边形顶点
        UpdatePolygonPoints(polygon, item.Points);

        AnnotationCanvas.Children.Add(polygon);
        item.UIElement = polygon;
    }

    private void UpdatePolygonElement(AnnotationItem item)
    {
        if (item != null)
        {
            var polygon = (PolygonModel)item;
            if (polygon.UIElement is Polygon thisPolygon)
            {
                var points = new List<Point>(polygon.Points);
                UpdatePolygonPoints(thisPolygon, points);
            }
        }
    }

    private void UpdatePolygonPoints(Polygon polygon, List<Avalonia.Point> points)
    {
        polygon.Points.Clear();
        foreach (var point in points)
        {
            polygon.Points.Add(point);
        }
    }

    private void UpdatePolygonPreviewLine(AnnotationItem item, Point currentPosition)
    {
        var polygon = item as PolygonModel;
        if (polygon == null || polygon.Points.Count < 1)
        {
            return;
        }

        // Remove any existing preview line to prevent multiple lines from being drawn.
        var existingPreviewLine = AnnotationCanvas.Children
            .OfType<Line>()
            .FirstOrDefault(l => l.Tag as string == "PreviewLine");

        if (existingPreviewLine != null)
        {
            AnnotationCanvas.Children.Remove(existingPreviewLine);
        }

        // Get the last point of the polygon.
        var lastPoint = polygon.Points.Last();

        // Create a new Line element for the preview.
        var previewLine = new Line
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>() { 4, 2 },
            StartPoint = new Point(lastPoint.X, lastPoint.Y),
            EndPoint = new Point(currentPosition.X, currentPosition.Y),
            IsHitTestVisible = false,
            // Use a tag to easily identify the preview line later.
            Tag = "PreviewLine"
        };

        // Add the new preview line to the canvas.
        AnnotationCanvas.Children.Add(previewLine);
    }

    private void RemovePolygonPreviewLine(AnnotationItem item)
    {
        // 查找并移除之前添加的预览线。
        var existingPreviewLine = AnnotationCanvas.Children
            .OfType<Line>()
            .FirstOrDefault(l => l.Tag as string == "PreviewLine");

        if (existingPreviewLine != null)
        {
            AnnotationCanvas.Children.Remove(existingPreviewLine);
        }
    }
    /// <summary>
    /// 创建点标注元素
    /// </summary>
    /// <param name="item"></param>

    private void CreatePointElement(AnnotationItem item)
    {
        if (item != null)
        {
            var point = (PointModel)item;
            var ellipse = new Ellipse
            {
                Tag = point.InstanceGuid,
                Width = 4,
                Height = 4,
                Fill = Brushes.Green,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ellipse, point.X - 4); // 居中
            Canvas.SetTop(ellipse, point.Y - 4);

            AnnotationCanvas.Children.Add(ellipse);
            point.UIElement = ellipse;
        }
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
            // 移除正在绘制的虚线
            RemovePolygonPreviewLine(_viewmodel.CurrentDrawingItem);
            _viewmodel.CurrentDrawingItem = null;
            _viewmodel.IsDrawingPolygon = false;
        }
    }

    #region 拖动功能相关方法

    /// <summary>
    /// 获取点击位置的标注元素
    /// </summary>
    /// <param name="position">点击位置</param>
    /// <returns>被点击的标注项，如果没有则返回null</returns>
    private AnnotationItem? GetHitAnnotationElement(Point position)
    {
        // 遍历所有标注项，检查点击位置是否在标注元素内
        foreach (var annotation in _viewmodel.CurrentImageAnnotations)
        {
            if (IsPointInAnnotation(position, annotation))
            {
                return annotation;
            }
        }
        return null;
    }

    /// <summary>
    /// 检查点是否在标注元素内
    /// </summary>
    /// <param name="point">检查的点</param>
    /// <param name="annotation">标注项</param>
    /// <returns>是否在元素内</returns>
    private bool IsPointInAnnotation(Point point, AnnotationItem annotation)
    {
        return annotation.Contains(point.X, point.Y);
    }

    /// <summary>
    /// 开始拖动标注元素
    /// </summary>
    /// <param name="item">要拖动的标注项</param>
    /// <param name="startPosition">拖动起始位置</param>
    private void StartDragging(AnnotationItem item, Point startPosition)
    {
        _isDragging = true;
        _draggingItem = item;
        _dragStartPoint = startPosition;

        // 记录元素的初始位置


        switch (item.AnnotationType)
        {
            case AnnotationToolEnum.Rectangle:
                if (item is RectangleModel rect)
                {
                    _elementStartPosition = new Point(rect.X, rect.Y);
                }
                break;
            case AnnotationToolEnum.Point:
                if (item is PointModel pointModel)
                {
                    _elementStartPosition = new Point(pointModel.X, pointModel.Y);
                }
                break;
            case AnnotationToolEnum.Polygon:
                if (item is PolygonModel polygon)
                {
                    // 对于多边形，保存所有原始点的位置
                    _originalPolygonPoints = [];
                    foreach (var point in polygon.Points)
                    {
                        _originalPolygonPoints.Add(new Avalonia.Point(point.X, point.Y));
                    }
                }
                break;
        }

        // 改变UI元素的视觉状态，表示正在拖动
        SetElementDraggingStyle(item, true);
    }

    /// <summary>
    /// 更新拖动过程
    /// </summary>
    /// <param name="currentPosition">当前鼠标位置</param>
    private void UpdateDragging(Point currentPosition)
    {
        if (_draggingItem == null) return;

        // 计算偏移量
        var deltaX = currentPosition.X - _dragStartPoint.X;
        var deltaY = currentPosition.Y - _dragStartPoint.Y;

        switch (_draggingItem.AnnotationType)
        {
            case AnnotationToolEnum.Rectangle:
                if (_draggingItem is RectangleModel rect)
                {
                    // 更新矩形位置
                    rect.X = _elementStartPosition.X + deltaX;
                    rect.Y = _elementStartPosition.Y + deltaY;
                    UpdateRectangleElement(_draggingItem);
                }
                break;

            case AnnotationToolEnum.Point:
                if(_draggingItem is PointModel point)
                {
                    // 更新点位置
                    point.X = _elementStartPosition.X + deltaX;
                    point.Y = _elementStartPosition.Y + deltaY;
                    UpdatePointElement(_draggingItem);
                }
                break;

            case AnnotationToolEnum.Polygon:
                // 更新多边形所有顶点位置
                UpdatePolygonPosition(_draggingItem, deltaX, deltaY);
                UpdatePolygonElement(_draggingItem);
                break;
        }
    }

    /// <summary>
    /// 更新多边形位置
    /// </summary>
    /// <param name="item">多边形标注项</param>
    /// <param name="deltaX">X轴偏移</param>
    /// <param name="deltaY">Y轴偏移</param>
    private void UpdatePolygonPosition(AnnotationItem item, double deltaX, double deltaY)
    {
        var polygon = (PolygonModel)item;
        if (_originalPolygonPoints == null || _originalPolygonPoints.Count != polygon.Points.Count)
            return;

        // 清空当前点集合
        polygon.Points.Clear();

        // 基于保存的原始点位置计算新位置
        for (int i = 0; i < _originalPolygonPoints.Count; i++)
        {
            var originalPoint = _originalPolygonPoints[i];
            var newPoint = new Avalonia.Point(
                originalPoint.X + deltaX,
                originalPoint.Y + deltaY
            );
            polygon.Points.Add(newPoint);
        }

        // 更新边界框
        //polygon.GetBoundingBox();
    }

    /// <summary>
    /// 完成拖动
    /// </summary>
    private void FinishDragging()
    {
        if (_draggingItem != null)
        {
            // 恢复UI元素的正常视觉状态
            SetElementDraggingStyle(_draggingItem, false);
        }

        // 清理拖动状态
        _isDragging = false;
        _draggingItem = null;
        _originalPolygonPoints = null;
    }
    /// <summary>
    /// 设置元素的拖动视觉状态
    /// </summary>
    /// <param name="item">标注项</param>
    /// <param name="isDragging">是否正在拖动</param>
    private void SetElementDraggingStyle(AnnotationItem item, bool isDragging)
    {
        var opacity = isDragging ? 0.7 : 1.0;

        if (item.UIElement != null)
        {
            var ploygon = item.UIElement as Polygon;
            if (ploygon != null)
            {
                ploygon.Opacity = opacity;
                ploygon.StrokeThickness = isDragging ? 3 : 2;
            }
        }
    }

    #endregion

}