using AutoTrainer.Enums;
using AutoTrainer.Models;
using AutoTrainer.Models.Crop;
using AutoTrainer.Models.Dataset;
using AutoTrainer.Models.Logging;
using AutoTrainer.Models.Training;
using AutoTrainer.Models.Annotation;
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

    // 调整手柄相关字段
    private List<Control> _resizeHandles = new();
    private List<Control> _vertexHandles = new();
    private ResizeHandle? _currentResizeHandle = null;
    private int? _selectedVertexIndex = null;
    // 用于跟踪之前选中的标注，以便在删除时清除对应的手柄和标签
    private string? _lastSelectedAnnotationGuid = null;
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

        // 订阅标签更新事件
        _viewmodel.OnAnnotationLabelUpdate += OnAnnotationLabelUpdate;

        // 订阅标注元素创建事件，用于在加载标注时添加标签
        _viewmodel.OnAnnotationElementCreated += OnAnnotationElementCreated;

        // 订阅 SelectedAnnotation 变化，用于在删除标注时清除手柄和标签
        _viewmodel.PropertyChanged += OnPropertyChanged;

        // 订阅 PointerWheelChanged 事件
        ImageScrollViewer.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
    }
    /// <summary>
    /// 用于处理滚轮水平滚动
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 获取滚轮变化量（Delta.Y 正值为向上滚动，负值为向下滚动）
            var delta = e.Delta.Y;
            // 设置水平滚动的步长（可以根据需要调整）
            var scrollStep = 50.0; // 每次滚动的像素距离
            // 计算新的水平偏移量
            var newOffset = scrollViewer.Offset.X - delta * scrollStep;
            // 应用新的偏移量
            scrollViewer.Offset = new Avalonia.Vector(newOffset, scrollViewer.Offset.Y);
            // 标记事件已处理，阻止默认行为
            e.Handled = true;
        }
    }

    /// <summary>
    /// 处理 ViewModel 属性变化事件
    /// </summary>
    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewmodel.SelectedAnnotation))
        {
            // 检查之前选中的标注是否仍然存在
            bool previousAnnotationStillExists = false;
            if (_lastSelectedAnnotationGuid != null)
            {
                previousAnnotationStillExists = _viewmodel.CurrentImageAnnotations
                    .Any(a => a.InstanceGuid == _lastSelectedAnnotationGuid);
            }

            // 如果之前选中的标注不再存在（被删除了），清除其手柄和标签
            if (_lastSelectedAnnotationGuid != null && !previousAnnotationStillExists)
            {
                ClearAllHandles();
                RemoveAnnotationLabel(_lastSelectedAnnotationGuid);
            }
            else
            {
                // 清除手柄
                ClearAllHandles();

                // 如果有新选中的标注，创建对应的手柄
                if (_viewmodel.SelectedAnnotation != null)
                {
                    if (_viewmodel.SelectedAnnotation is RectangleModel rect)
                    {
                        CreateResizeHandles(rect);
                    }
                    else if (_viewmodel.SelectedAnnotation is PolygonModel polygon)
                    {
                        CreateVertexHandles(polygon);
                    }
                }
            }

            // 更新当前选中的标注 GUID
            _lastSelectedAnnotationGuid = _viewmodel.SelectedAnnotation?.InstanceGuid;
        }
    }

    /// <summary>
    /// 移除指定标注的标签
    /// </summary>
    private void RemoveAnnotationLabel(string annotationGuid)
    {
        var label = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{annotationGuid}_label");
        if (label != null)
        {
            AnnotationCanvas.Children.Remove(label);
        }
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

        // 首先检查是否点击了已存在的标注元素（用于拖动或编辑）
        var hitElement = GetHitAnnotationElement(position);
        if (hitElement != null)
        {
            // 清除所有手柄
            ClearAllHandles();

            StartDragging(hitElement, position);
            _viewmodel?.OnAnnotationSelected?.Invoke(hitElement);
            e.Handled = true;

            // 根据标注类型显示对应的手柄
            if (hitElement is RectangleModel rect)
            {
                CreateResizeHandles(rect);
            }
            else if (hitElement is PolygonModel polygon)
            {
                CreateVertexHandles(polygon);
            }
            return;
        }

        // 如果点击了空白区域，清除选择和手柄
        if (_viewmodel.SelectedAnnotation != null)
        {
            ClearAllHandles();
            _viewmodel.SelectedAnnotation = null;
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

        // 获取选定类别的颜色和名称
        var selectedColor = _viewmodel.SelectedCategory?.Color ?? Colors.Red;
        var selectedClassName = _viewmodel.SelectedCategory?.Name ?? "Default";

        // 创建矩形标注项
        _viewmodel.CurrentDrawingItem = new RectangleModel(startPoint.X, startPoint.Y, 0, 0)
        {
            AnnotationType = AnnotationToolEnum.Rectangle,
            ClassName = selectedClassName,
            DisplayColor = selectedColor
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

        // 获取选定类别的颜色和名称
        var selectedColor = _viewmodel.SelectedCategory?.Color ?? Colors.Blue;
        var selectedClassName = _viewmodel.SelectedCategory?.Name ?? "Default";

        // 创建多边形标注项
        var points = new List<Point> { startPoint };
        _viewmodel.CurrentDrawingItem = new PolygonModel
        {
            AnnotationType = AnnotationToolEnum.Polygon,
            Points = points,
            ClassName = selectedClassName,
            DisplayColor = selectedColor
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

        // 获取选定类别的颜色和名称
        var selectedColor = _viewmodel.SelectedCategory?.Color ?? Colors.Green;
        var selectedClassName = _viewmodel.SelectedCategory?.Name ?? "Default";

        // 创建点标注
        var pointAnnotation = new PointModel(clickPoint.X, clickPoint.Y)
        {
            AnnotationType = AnnotationToolEnum.Point,
            ClassName = selectedClassName,
            DisplayColor = selectedColor
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
        var color = item.DisplayColor;
        var rectangle = new Rectangle
        {
            Tag = item.InstanceGuid,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(rectangle, item.X);
        Canvas.SetTop(rectangle, item.Y);
        rectangle.Width = item.Width;
        rectangle.Height = item.Height;

        // 添加类别标签
        AddCategoryLabel(item, color);

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
            item.UpdateBoundingBoxInfo();
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
            item.UpdateBoundingBoxInfo();
        }
    }
    /// <summary>
    /// 创建多边形标注
    /// </summary>
    /// <param name="item"></param>
    private void CreatePolygonElement(PolygonModel item)
    {
        var color = item.DisplayColor;
        var polygon = new Polygon
        {
            Tag = item.InstanceGuid,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(color) { Opacity = 0.1 },
            IsHitTestVisible = false,
        };

        // 设置多边形顶点
        polygon.Points = [.. item.Points];

        // 添加类别标签
        AddCategoryLabel(item, color);

        AnnotationCanvas.Children.Add(polygon);
        item.UIElement = polygon;
    }

    private void UpdatePolygonElement(AnnotationItem item)
    {
        if (item != null)
        {
            var polygon = (PolygonModel)item;
            //RecreatePolygonElement(polygon);
            if (polygon.UIElement is Polygon thisPolygon)
            {
                var points = new List<Point>(polygon.Points);
                thisPolygon.Points = [.. points];
            }
            item.UpdateBoundingBoxInfo();
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
            var color = item.DisplayColor;
            var ellipse = new Ellipse
            {
                Tag = point.InstanceGuid,
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ellipse, point.X - 4); // 居中
            Canvas.SetTop(ellipse, point.Y - 4);

            // 添加类别标签
            AddCategoryLabel(item, color);

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

        // 同时移除类别标签
        var label = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{item.InstanceGuid}_label");
        if (label != null)
        {
            AnnotationCanvas.Children.Remove(label);
        }
    }

    /// <summary>
    /// 添加类别标签
    /// </summary>
    private void AddCategoryLabel(AnnotationItem item, Color color)
    {
        if (string.IsNullOrEmpty(item.ClassName))
            return;

        // 检查是否已经存在该标签（避免重复添加）
        var existingLabel = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{item.InstanceGuid}_label");
        if (existingLabel != null)
            return; // 标签已存在，不需要重复添加

        var boundingBox = item.GetBoundingBox();

        var textBlock = new TextBlock
        {
            Text = item.ClassName,
            Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Colors.White) { Opacity = 0.7 },
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(4, 2, 4, 2),
            Tag = $"{item.InstanceGuid}_label",
            IsHitTestVisible = false
        };

        Canvas.SetLeft(textBlock, boundingBox.X);
        Canvas.SetTop(textBlock, boundingBox.Y - 20); // 在标注上方显示

        AnnotationCanvas.Children.Add(textBlock);
    }

    /// <summary>
    /// 更新标注标签位置
    /// </summary>
    private void UpdateAnnotationLabel(AnnotationItem item)
    {
        if (string.IsNullOrEmpty(item.ClassName))
            return;

        // 查找现有的标签
        var label = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{item.InstanceGuid}_label");

        if (label != null)
        {
            var boundingBox = item.GetBoundingBox();
            Canvas.SetLeft(label, boundingBox.X);
            Canvas.SetTop(label, boundingBox.Y - 20);
        }
    }

    /// <summary>
    /// 更新标注标签文本和颜色（用于类别切换）
    /// </summary>
    private void OnAnnotationLabelUpdate(AnnotationItem item)
    {
        // 查找现有的标签
        var label = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{item.InstanceGuid}_label");

        if (label != null)
        {
            // 更新文本
            label.Text = item.ClassName ?? string.Empty;

            // 更新颜色
            label.Foreground = new SolidColorBrush(item.DisplayColor);
        }
    }

    /// <summary>
    /// 处理标注元素创建事件（用于在加载标注时添加标签）
    /// 注意：此方法仅用于从已保存的数据加载标注时添加标签，
    /// 新绘制的标注应通过 AddCategoryLabel 添加标签
    /// </summary>
    private void OnAnnotationElementCreated(AnnotationItem item)
    {
        // 检查是否已经存在该标签（避免重复添加）
        var existingLabel = AnnotationCanvas.Children.OfType<TextBlock>()
            .FirstOrDefault(l => l.Tag?.ToString() == $"{item.InstanceGuid}_label");

        if (existingLabel != null)
            return; // 标签已存在，不需要重复添加

        if (string.IsNullOrEmpty(item.ClassName))
            return;

        var color = item.DisplayColor;
        var boundingBox = item.GetBoundingBox();

        var textBlock = new TextBlock
        {
            Text = item.ClassName,
            Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Colors.White) { Opacity = 0.7 },
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(4, 2, 4, 2),
            Tag = $"{item.InstanceGuid}_label",
            IsHitTestVisible = false
        };

        Canvas.SetLeft(textBlock, boundingBox.X);
        Canvas.SetTop(textBlock, boundingBox.Y - 20);

        AnnotationCanvas.Children.Add(textBlock);
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

        // 清除所有手柄
        ClearAllHandles();
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


        switch (item)
        {
            case RectangleModel rect:
                {
                    _elementStartPosition = new Point(rect.X, rect.Y);
                }
                break;
            case PointModel pointModel:
                {
                    _elementStartPosition = new Point(pointModel.X, pointModel.Y);
                }
                break;
            case PolygonModel polygon:
                {
                    // 对于多边形，保存所有原始点的位置
                    _originalPolygonPoints = [];
                    foreach (var point in polygon.Points)
                    {
                        _originalPolygonPoints.Add(new Point(point.X, point.Y));
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

        switch (_draggingItem)
        {
            case RectangleModel rect:
                {
                    // 更新矩形位置
                    rect.X = _elementStartPosition.X + deltaX;
                    rect.Y = _elementStartPosition.Y + deltaY;
                    UpdateRectangleElement(_draggingItem);
                    // 更新标签位置
                    UpdateAnnotationLabel(_draggingItem);
                    // 更新调整手柄位置
                    if (_resizeHandles.Count > 0)
                    {
                        UpdateResizeHandlesPositions(rect);
                    }
                }
                break;

            case PointModel point:
                {
                    // 更新点位置
                    point.X = _elementStartPosition.X + deltaX;
                    point.Y = _elementStartPosition.Y + deltaY;
                    UpdatePointElement(_draggingItem);
                    // 更新标签位置
                    UpdateAnnotationLabel(_draggingItem);
                }
                break;

            case PolygonModel polygon:
                // 更新多边形数据模型所有位置
                UpdatePolygonPosition(_draggingItem, deltaX, deltaY);
                // 更新UI元素位置
                UpdatePolygonElement(_draggingItem);
                // 更新标签位置
                UpdateAnnotationLabel(_draggingItem);
                // 更新顶点手柄位置
                if (_vertexHandles.Count > 0)
                {
                    UpdateAllVertexHandlesPositions(polygon);
                }
                break;
        }
    }

    /// <summary>
    /// 更新所有顶点手柄位置
    /// </summary>
    private void UpdateAllVertexHandlesPositions(PolygonModel polygon)
    {
        var handleSize = 10.0;

        for (int i = 0; i < polygon.Points.Count && i < _vertexHandles.Count; i++)
        {
            var point = polygon.Points[i];
            var handle = _vertexHandles[i];
            Canvas.SetLeft(handle, point.X - handleSize / 2);
            Canvas.SetTop(handle, point.Y - handleSize / 2);
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
        polygon.GetBoundingBox();
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
            if (item.UIElement is Polygon ploygon)
            {
                ploygon.Opacity = opacity;
                ploygon.StrokeThickness = isDragging ? 3 : 2;
            }
        }
    }

    #endregion

    #region 调整大小和顶点编辑功能

    /// <summary>
    /// 创建矩形调整手柄
    /// </summary>
    private void CreateResizeHandles(RectangleModel rect)
    {
        ClearResizeHandles();
        var handleSize = 8.0;
        var bbox = rect.GetBoundingBox();

        var positions = new Dictionary<HandleType, Point>
        {
            { HandleType.TopLeft, new Point(bbox.X, bbox.Y) },
            { HandleType.Top, new Point(bbox.X + bbox.Width / 2, bbox.Y) },
            { HandleType.TopRight, new Point(bbox.X + bbox.Width, bbox.Y) },
            { HandleType.Right, new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height / 2) },
            { HandleType.BottomRight, new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height) },
            { HandleType.Bottom, new Point(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height) },
            { HandleType.BottomLeft, new Point(bbox.X, bbox.Y + bbox.Height) },
            { HandleType.Left, new Point(bbox.X, bbox.Y + bbox.Height / 2) }
        };

        // 定义每个手柄对应的光标
        var cursorMap = new Dictionary<HandleType, StandardCursorType>
        {
            { HandleType.TopLeft, StandardCursorType.TopLeftCorner },
            { HandleType.Top, StandardCursorType.TopSide },
            { HandleType.TopRight, StandardCursorType.TopRightCorner },
            { HandleType.Right, StandardCursorType.RightSide },
            { HandleType.BottomRight, StandardCursorType.BottomRightCorner },
            { HandleType.Bottom, StandardCursorType.BottomSide },
            { HandleType.BottomLeft, StandardCursorType.BottomLeftCorner },
            { HandleType.Left, StandardCursorType.LeftSide }
        };

        foreach (var (type, pos) in positions)
        {
            var handle = new Rectangle
            {
                Width = handleSize,
                Height = handleSize,
                Fill = Brushes.White,
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Tag = new ResizeHandle(type, new Rect(pos.X - handleSize / 2, pos.Y - handleSize / 2, handleSize, handleSize)),
                IsHitTestVisible = true,
                ZIndex = 100,
                Cursor = new Cursor(cursorMap[type])
            };
            Canvas.SetLeft(handle, pos.X - handleSize / 2);
            Canvas.SetTop(handle, pos.Y - handleSize / 2);

            handle.PointerPressed += OnResizeHandlePressed;
            handle.PointerMoved += OnResizeHandleMoved;
            handle.PointerReleased += OnResizeHandleReleased;

            AnnotationCanvas.Children.Add(handle);
            _resizeHandles.Add(handle);
        }
    }

    /// <summary>
    /// 创建多边形顶点手柄
    /// </summary>
    private void CreateVertexHandles(PolygonModel polygon)
    {
        ClearVertexHandles();
        var handleSize = 10.0;

        for (int i = 0; i < polygon.Points.Count; i++)
        {
            var point = polygon.Points[i];
            var handle = new Ellipse
            {
                Width = handleSize,
                Height = handleSize,
                Fill = Brushes.White,
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Tag = i,
                IsHitTestVisible = true,
                ZIndex = 100,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            Canvas.SetLeft(handle, point.X - handleSize / 2);
            Canvas.SetTop(handle, point.Y - handleSize / 2);

            handle.PointerPressed += OnVertexHandlePressed;
            handle.PointerMoved += OnVertexHandleMoved;
            handle.PointerReleased += OnVertexHandleReleased;

            AnnotationCanvas.Children.Add(handle);
            _vertexHandles.Add(handle);
        }
    }

    /// <summary>
    /// 清除调整手柄
    /// </summary>
    private void ClearResizeHandles()
    {
        foreach (var h in _resizeHandles)
        {
            h.PointerPressed -= OnResizeHandlePressed;
            h.PointerMoved -= OnResizeHandleMoved;
            h.PointerReleased -= OnResizeHandleReleased;
            AnnotationCanvas.Children.Remove(h);
        }
        _resizeHandles.Clear();
    }

    /// <summary>
    /// 清除顶点手柄
    /// </summary>
    private void ClearVertexHandles()
    {
        foreach (var h in _vertexHandles)
        {
            h.PointerPressed -= OnVertexHandlePressed;
            h.PointerMoved -= OnVertexHandleMoved;
            h.PointerReleased -= OnVertexHandleReleased;
            AnnotationCanvas.Children.Remove(h);
        }
        _vertexHandles.Clear();
    }

    /// <summary>
    /// 清除所有手柄
    /// </summary>
    private void ClearAllHandles()
    {
        ClearResizeHandles();
        ClearVertexHandles();
    }

    /// <summary>
    /// 调整手柄按下事件
    /// </summary>
    private void OnResizeHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Rectangle handle || handle.Tag is not ResizeHandle resizeHandle) return;

        _currentResizeHandle = resizeHandle;
        _isDragging = true;
        _dragStartPoint = e.GetPosition(AnnotationCanvas);
        _draggingItem = _viewmodel.SelectedAnnotation;

        // 捕获光标，确保拖动连续性
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    /// <summary>
    /// 调整手柄移动事件
    /// </summary>
    private void OnResizeHandleMoved(object? sender, PointerEventArgs e)
    {
        if (_currentResizeHandle == null || _viewmodel.SelectedAnnotation is not RectangleModel rect || _draggingItem == null) return;

        var currentPos = e.GetPosition(AnnotationCanvas);
        var deltaX = currentPos.X - _dragStartPoint.X;
        var deltaY = currentPos.Y - _dragStartPoint.Y;

        var handleType = _currentResizeHandle.Type;

        // 根据手柄类型调整矩形
        switch (handleType)
        {
            case HandleType.Right:
                rect.Width = Math.Max(10, rect.Width + deltaX);
                break;
            case HandleType.Bottom:
                rect.Height = Math.Max(10, rect.Height + deltaY);
                break;
            case HandleType.Left:
                var newX = rect.X + deltaX;
                if (newX >= 0)
                {
                    rect.Width = Math.Max(10, rect.Width - deltaX);
                    rect.X = newX;
                }
                break;
            case HandleType.Top:
                var newY = rect.Y + deltaY;
                if (newY >= 0)
                {
                    rect.Height = Math.Max(10, rect.Height - deltaY);
                    rect.Y = newY;
                }
                break;
            case HandleType.BottomRight:
                rect.Width = Math.Max(10, rect.Width + deltaX);
                rect.Height = Math.Max(10, rect.Height + deltaY);
                break;
            case HandleType.BottomLeft:
                var newXBL = rect.X + deltaX;
                if (newXBL >= 0)
                {
                    rect.Width = Math.Max(10, rect.Width - deltaX);
                    rect.X = newXBL;
                }
                rect.Height = Math.Max(10, rect.Height + deltaY);
                break;
            case HandleType.TopRight:
                var newYTR = rect.Y + deltaY;
                if (newYTR >= 0)
                {
                    rect.Height = Math.Max(10, rect.Height - deltaY);
                    rect.Y = newYTR;
                }
                rect.Width = Math.Max(10, rect.Width + deltaX);
                break;
            case HandleType.TopLeft:
                var newXTL = rect.X + deltaX;
                var newYTL = rect.Y + deltaY;
                if (newXTL >= 0)
                {
                    rect.Width = Math.Max(10, rect.Width - deltaX);
                    rect.X = newXTL;
                }
                if (newYTL >= 0)
                {
                    rect.Height = Math.Max(10, rect.Height - deltaY);
                    rect.Y = newYTL;
                }
                break;
        }

        UpdateRectangleElement(rect);
        UpdateResizeHandlesPositions(rect); // 更新手柄位置而不是重新创建
        UpdateAnnotationLabel(rect); // 更新标签位置
        _dragStartPoint = currentPos;
        e.Handled = true;
    }

    /// <summary>
    /// 调整手柄释放事件
    /// </summary>
    private void OnResizeHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _currentResizeHandle = null;
        _isDragging = false;
        _draggingItem = null;
        e.Handled = true;
    }

    /// <summary>
    /// 顶点手柄按下事件
    /// </summary>
    private void OnVertexHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Ellipse handle || handle.Tag is not int index) return;

        _selectedVertexIndex = index;
        _isDragging = true;
        _draggingItem = _viewmodel.SelectedAnnotation;

        // 捕获光标，确保拖动连续性
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    /// <summary>
    /// 顶点手柄移动事件
    /// </summary>
    private void OnVertexHandleMoved(object? sender, PointerEventArgs e)
    {
        if (_viewmodel.SelectedAnnotation is not PolygonModel polygon || _selectedVertexIndex == null) return;

        var pos = e.GetPosition(AnnotationCanvas);
        polygon.Points[_selectedVertexIndex.Value] = pos;

        UpdatePolygonElement(polygon);
        UpdateVertexHandlePosition(_selectedVertexIndex.Value, pos); // 更新手柄位置而不是重新创建
        UpdateAnnotationLabel(polygon); // 更新标签位置
        e.Handled = true;
    }

    /// <summary>
    /// 顶点手柄释放事件
    /// </summary>
    private void OnVertexHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _selectedVertexIndex = null;
        _isDragging = false;
        _draggingItem = null;
        e.Handled = true;
    }

    /// <summary>
    /// 更新矩形调整手柄位置（不重新创建）
    /// </summary>
    private void UpdateResizeHandlesPositions(RectangleModel rect)
    {
        var bbox = rect.GetBoundingBox();
        var handleSize = 8.0;

        var positions = new Dictionary<HandleType, Point>
        {
            { HandleType.TopLeft, new Point(bbox.X, bbox.Y) },
            { HandleType.Top, new Point(bbox.X + bbox.Width / 2, bbox.Y) },
            { HandleType.TopRight, new Point(bbox.X + bbox.Width, bbox.Y) },
            { HandleType.Right, new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height / 2) },
            { HandleType.BottomRight, new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height) },
            { HandleType.Bottom, new Point(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height) },
            { HandleType.BottomLeft, new Point(bbox.X, bbox.Y + bbox.Height) },
            { HandleType.Left, new Point(bbox.X, bbox.Y + bbox.Height / 2) }
        };

        foreach (var handle in _resizeHandles.OfType<Rectangle>())
        {
            if (handle.Tag is ResizeHandle resizeHandle)
            {
                if (positions.TryGetValue(resizeHandle.Type, out var pos))
                {
                    Canvas.SetLeft(handle, pos.X - handleSize / 2);
                    Canvas.SetTop(handle, pos.Y - handleSize / 2);
                    // 更新Bounds信息
                    resizeHandle.Bounds = new Rect(pos.X - handleSize / 2, pos.Y - handleSize / 2, handleSize, handleSize);
                }
            }
        }
    }

    /// <summary>
    /// 更新单个顶点手柄位置（不重新创建）
    /// </summary>
    private void UpdateVertexHandlePosition(int vertexIndex, Point newPosition)
    {
        var handleSize = 10.0;

        if (vertexIndex >= 0 && vertexIndex < _vertexHandles.Count)
        {
            var handle = _vertexHandles[vertexIndex];
            Canvas.SetLeft(handle, newPosition.X - handleSize / 2);
            Canvas.SetTop(handle, newPosition.Y - handleSize / 2);
        }
    }

    #endregion

}