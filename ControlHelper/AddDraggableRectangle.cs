using AutoTrainer.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ControlHelper
{
    public class DraggableRectangle
    {
        private static Point? dragStart;
        private static bool isDragging = false;
        public static void AddDraggableRectangle(SingleCorpArea area, Canvas DragCanvas)
        {
            var container = new Canvas();

            var rectangle = new Rectangle
            {
                Width = area.X2 - area.X1,
                Height = area.Y2 - area.Y1,
                Fill = new SolidColorBrush(Colors.Red) { Opacity = 0.3 },
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 2
            };

            var resizeThumb = new Thumb
            {
                Width = 20,
                Height = 20,
                Foreground = new SolidColorBrush(Colors.Red),
                Background = new SolidColorBrush(Colors.White),
                Cursor = new Cursor(StandardCursorType.BottomRightCorner)
            };

            container.Children.Add(rectangle);
            container.Children.Add(resizeThumb);

            Canvas.SetLeft(container, area.X1);
            Canvas.SetTop(container, area.Y1);
            Canvas.SetRight(resizeThumb, 0);
            Canvas.SetBottom(resizeThumb, 0);

            // 拖动事件
            EventHandler<PointerEventArgs> pointerMovedHandler = null;

            container.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
                {
                    isDragging = true;
                    dragStart = e.GetPosition(DragCanvas);
                    container.Cursor = new Cursor(StandardCursorType.Hand);
                    e.Pointer.Capture(container);

                    pointerMovedHandler = (s2, e2) =>
                    {
                        if (isDragging)
                        {
                            var currentPoint = e2.GetPosition(DragCanvas);
                            var deltaX = currentPoint.X - dragStart.Value.X;
                            var deltaY = currentPoint.Y - dragStart.Value.Y;

                            var newLeft = Canvas.GetLeft(container) + deltaX;
                            var newTop = Canvas.GetTop(container) + deltaY;

                            // 限制在图片范围内
                            newLeft = Math.Max(0, Math.Min(newLeft, DragCanvas.Bounds.Width - container.Bounds.Width));
                            newTop = Math.Max(0, Math.Min(newTop, DragCanvas.Bounds.Height - container.Bounds.Height));

                            Canvas.SetLeft(container, newLeft);
                            Canvas.SetTop(container, newTop);

                            // 更新模型
                            area.X1 = (int)newLeft;
                            area.Y1 = (int)newTop;
                            area.X2 = (int)(newLeft + rectangle.Width);
                            area.Y2 = (int)(newTop + rectangle.Height);

                            dragStart = currentPoint;
                        }
                    };

                    container.PointerMoved += pointerMovedHandler;
                }
            };

            container.PointerReleased += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    dragStart = null;
                    container.Cursor = new Cursor(StandardCursorType.Arrow);
                    //e.Pointer.Captured = null;  // 释放指针捕获

                    if (pointerMovedHandler != null)
                    {
                        container.PointerMoved -= pointerMovedHandler;
                        //pointerMovedHandler = null;
                    }
                }
            };

            // 调整大小事件
            double originalWidth = 0;
            double originalHeight = 0;
            Point resizeStart = new Point();
            bool isResizing = false;

            resizeThumb.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(resizeThumb).Properties.IsLeftButtonPressed)
                {
                    isResizing = true;
                    originalWidth = rectangle.Width;
                    originalHeight = rectangle.Height;
                    resizeStart = e.GetPosition(DragCanvas);
                    e.Pointer.Capture(resizeThumb);
                }
            };

            resizeThumb.PointerMoved += (s, e) =>
            {
                if (isResizing && e.GetCurrentPoint(resizeThumb).Properties.IsLeftButtonPressed)
                {
                    var currentPoint = e.GetPosition(DragCanvas);
                    var deltaX = currentPoint.X - resizeStart.X;
                    var deltaY = currentPoint.Y - resizeStart.Y;

                    var newWidth = Math.Max(20, originalWidth + deltaX);
                    var newHeight = Math.Max(20, originalHeight + deltaY);

                    // 确保不超出图片边界
                    var containerLeft = Canvas.GetLeft(container);
                    var containerTop = Canvas.GetTop(container);

                    if (containerLeft + newWidth <= DragCanvas.Bounds.Width &&
                        containerTop + newHeight <= DragCanvas.Bounds.Height)
                    {
                        rectangle.Width = newWidth;
                        rectangle.Height = newHeight;
                        container.Width = newWidth;
                        container.Height = newHeight;

                        // 更新模型
                        area.X2 = area.X1 + (int)newWidth;
                        area.Y2 = area.Y1 + (int)newHeight;
                    }
                }
            };

            resizeThumb.PointerReleased += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    //e.Pointer.Captured = null;  // 释放指针捕获
                    originalWidth = 0;
                    originalHeight = 0;
                }
            };

            resizeThumb.PointerCaptureLost += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    originalWidth = 0;
                    originalHeight = 0;
                }
            };
            DragCanvas.Children.Add(container);
        }
    }
}
