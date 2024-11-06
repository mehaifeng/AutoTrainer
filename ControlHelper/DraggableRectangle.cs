using AutoTrainer.Models;
using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ControlHelper
{
    public class DraggableRectangle
    {
        private static bool isDragging = false;
        private static bool isResizing = false;
        private static double originalWidth;
        private static double originalHeight;

        public static void AddDraggableRectangle(SingleCropArea area, Canvas DragCanvas,
            Bitmap sourceImage, Image displayImage, string label = "")
        {
            // 计算显示坐标和原始图像坐标之间的比例
            double scaleX = sourceImage.PixelSize.Width / displayImage.Bounds.Width;
            double scaleY = sourceImage.PixelSize.Height / displayImage.Bounds.Height;

            // 坐标转换函数：从原始图像坐标到显示坐标
            Point ConvertToDisplayCoordinates(double sourceX, double sourceY)
            {
                double displayX = sourceX / scaleX;
                double displayY = sourceY / scaleY;
                return new Point(displayX, displayY);
            }

            // 坐标转换函数：从显示坐标到原始图像坐标
            Point ConvertToSourceCoordinates(double displayX, double displayY)
            {
                double sourceX = displayX * scaleX;
                double sourceY = displayY * scaleY;
                return new Point(sourceX, sourceY);
            }

            var container = new Canvas();

            // 转换初始坐标到显示坐标
            var startPoint = ConvertToDisplayCoordinates(area.X1, area.Y1);
            var endPoint = ConvertToDisplayCoordinates(area.X2, area.Y2);

            // 主矩形
            var rectangle = new Rectangle
            {
                Width = endPoint.X - startPoint.X,
                Height = endPoint.Y - startPoint.Y,
                Fill = new SolidColorBrush(Colors.Red) { Opacity = 0.3 },
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 2
            };

            // 文本标签
            var textBox = new TextBox
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.Yellow),
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(2),
                FontSize = 12,
                Height = 16,
                MaxLines = 1,
            };

            // 调整大小的控件
            var resizeHandle = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.Red),
                Cursor = new Cursor(StandardCursorType.BottomRightCorner)
            };

            container.Children.Add(rectangle);
            container.Children.Add(textBox);
            container.Children.Add(resizeHandle);

            Canvas.SetLeft(container, startPoint.X);
            Canvas.SetTop(container, startPoint.Y);

            void UpdateResizeHandlePosition()
            {
                Canvas.SetLeft(resizeHandle, rectangle.Width - resizeHandle.Width);
                Canvas.SetTop(resizeHandle, rectangle.Height - resizeHandle.Height);
            }
            UpdateResizeHandlePosition();

            // 存储对应的CropConfig引用
            container.Tag = area;

            //文本改变事件处理
            textBox.TextChanged += (s, e) =>
            {
                var cropArea = container.Tag as SingleCropArea;
                if (cropArea != null)
                {
                    cropArea.Name = textBox.Text;
                }
            };

            // 拖动事件处理
            Point dragStart = new Point();
            EventHandler<PointerEventArgs> pointerMovedHandler = null;

            container.PointerPressed += (s, e) =>
                {
                    if (e.GetCurrentPoint(container).Properties.IsLeftButtonPressed && !isResizing)
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
                                var deltaX = currentPoint.X - dragStart.X;
                                var deltaY = currentPoint.Y - dragStart.Y;

                                var newLeft = Canvas.GetLeft(container) + deltaX;
                                var newTop = Canvas.GetTop(container) + deltaY;

                                // 限制在图像显示区域内
                                newLeft = Math.Max(0, Math.Min(newLeft, displayImage.Bounds.Width - container.Bounds.Width));
                                newTop = Math.Max(0, Math.Min(newTop, displayImage.Bounds.Height - container.Bounds.Height));

                                Canvas.SetLeft(container, newLeft);
                                Canvas.SetTop(container, newTop);

                                // 更新原始坐标
                                var sourcePoint = ConvertToSourceCoordinates(newLeft, newTop);
                                var sourceEndPoint = ConvertToSourceCoordinates(newLeft + rectangle.Width, newTop + rectangle.Height);
                                // 获取存储的引用并更新
                                var cropArea = container.Tag as SingleCropArea;
                                if (cropArea != null)
                                {
                                    cropArea.X1 = (int)Math.Round(sourcePoint.X);
                                    cropArea.X2 = (int)Math.Round(sourceEndPoint.X);
                                    cropArea.Y1 = (int)Math.Round(sourcePoint.Y);
                                    cropArea.Y2 = (int)Math.Round(sourceEndPoint.Y);
                                }
                                //area.X1 = (int)Math.Round(sourcePoint.X);
                                //area.X2 = (int)Math.Round(sourceEndPoint.X);
                                //area.Y2 = (int)Math.Round(sourceEndPoint.Y);

                                dragStart = currentPoint;
                            }
                        };

                        container.PointerMoved += pointerMovedHandler;
                    }
                };

            // 调整大小事件处理
            Point resizeStartPoint = new Point();
            EventHandler<PointerEventArgs> resizeMovedHandler = null;

            resizeHandle.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(resizeHandle).Properties.IsLeftButtonPressed)
                {
                    isResizing = true;
                    isDragging = false;
                    resizeStartPoint = e.GetPosition(DragCanvas);
                    originalWidth = rectangle.Width;
                    originalHeight = rectangle.Height;
                    e.Pointer.Capture(resizeHandle);

                    resizeMovedHandler = (s2, e2) =>
                    {
                        if (isResizing)
                        {
                            var currentPoint = e2.GetPosition(DragCanvas);
                            var deltaX = currentPoint.X - resizeStartPoint.X;
                            var deltaY = currentPoint.Y - resizeStartPoint.Y;

                            var containerLeft = Canvas.GetLeft(container);
                            var containerTop = Canvas.GetTop(container);

                            // 确保不超出图像显示区域
                            var newWidth = Math.Max(20, Math.Min(originalWidth + deltaX,
                                displayImage.Bounds.Width - containerLeft));
                            var newHeight = Math.Max(20, Math.Min(originalHeight + deltaY,
                                displayImage.Bounds.Height - containerTop));

                            rectangle.Width = newWidth;
                            rectangle.Height = newHeight;

                            // 更新原始坐标
                            var sourceEndPoint = ConvertToSourceCoordinates(containerLeft + newWidth, containerTop + newHeight);
                            //area.X2 = (int)Math.Round(sourceEndPoint.X);
                            //area.Y2 = (int)Math.Round(sourceEndPoint.Y);
                            // 获取存储的引用并更新
                            var cropArea = container.Tag as SingleCropArea;
                            if (cropArea != null)
                            {
                                cropArea.X2 = (int)Math.Round(sourceEndPoint.X);
                                cropArea.Y2 = (int)Math.Round(sourceEndPoint.Y);
                            }
                            UpdateResizeHandlePosition();
                        }
                    };

                    resizeHandle.PointerMoved += resizeMovedHandler;
                }
            };

            // 释放事件处理
            container.PointerReleased += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    container.Cursor = new Cursor(StandardCursorType.Arrow);
                    e.Pointer.Capture(null);
                    if (pointerMovedHandler != null)
                    {
                        container.PointerMoved -= pointerMovedHandler;
                    }
                }
            };

            resizeHandle.PointerReleased += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    e.Pointer.Capture(null);
                    if (resizeMovedHandler != null)
                    {
                        resizeHandle.PointerMoved -= resizeMovedHandler;
                    }
                }
            };

            DragCanvas.Children.Add(container);
        }
    }
}
