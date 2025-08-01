using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public partial class AnnotationItem:ObservableObject
    {
        public string AnnotataionItemGuid = Guid.NewGuid().ToString();
        /// <summary>
        /// 标注的类别名称。
        /// </summary>
        [ObservableProperty]
        private string className = string.Empty;

        // 对于矩形和点标注：表示其位置和尺寸。
        // 对于多边形标注：表示其边界框的位置和尺寸。
        [ObservableProperty]
        private double x = 0;

        [ObservableProperty]
        private double y = 0;

        [ObservableProperty]
        private double width = 0;

        [ObservableProperty]
        private double height = 0;

        /// <summary>
        /// 多边形标注,存储多边形的顶点集合。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Avalonia.Point> points = new ObservableCollection<Avalonia.Point>();

        /// <summary>
        /// 标注的工具类型（矩形、多边形或点）。
        /// </summary>
        [ObservableProperty]
        private AnnotationTool toolType = AnnotationTool.Rectangle;

        /// <summary>
        /// 标注是否可见
        /// </summary>
        [ObservableProperty]
        private bool isVisible = true;

        /// <summary>
        /// 标注是否被选中
        /// </summary>
        [ObservableProperty]
        private bool isSelected = false;

        /// <summary>
        /// 获取标注的边界信息
        /// </summary>
        public string BoundsInfo => $"({X:F0}, {Y:F0}) - {Width:F0}x{Height:F0}";


        public AnnotationItem()
        {
            // 默认构造函数，可根据需要初始化属性
        }

        /// <summary>
        /// 创建矩形标注。
        /// </summary>
        /// <param name="x">矩形左上角的X坐标。</param>
        /// <param name="y">矩形左上角的Y坐标。</param>
        /// <param name="width">矩形的宽度。</param>
        /// <param name="height">矩形的高度。</param>
        /// <param name="className">标注的类别名称。</param>
        public AnnotationItem(double x, double y, double width, double height, string className)
        {
            ToolType = AnnotationTool.Rectangle;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ClassName = className;
        }

        /// <summary>
        /// 创建点标注。
        /// </summary>
        /// <param name="x">点的X坐标。</param>
        /// <param name="y">点的Y坐标。</param>
        /// <param name="className">标注的类别名称。</param>
        public AnnotationItem(double x, double y, string className)
        {
            ToolType = AnnotationTool.Point;
            X = x;
            Y = y;
            Width = 0; // 点通常没有宽度和高度，或者用一个固定的小尺寸表示
            Height = 0;
            ClassName = className;
        }

        /// <summary>
        /// 创建多边形标注。
        /// </summary>
        /// <param name="polygonPoints">多边形的顶点集合。</param>
        /// <param name="className">标注的类别名称。</param>
        public AnnotationItem(IEnumerable<Avalonia.Point> polygonPoints, string className)
        {
            ToolType = AnnotationTool.Polygon;
            Points = new ObservableCollection<Avalonia.Point>(polygonPoints);
            ClassName = className;
            UpdateBoundingBox(); // 根据多边形顶点计算其边界框
        }

        /// <summary>
        /// 根据多边形的顶点更新其边界框
        /// </summary>
        public void UpdateBoundingBox()
        {
            if (ToolType == AnnotationTool.Polygon && Points != null && Points.Any())
            {
                double minX = Points.Min(p => p.X);
                double minY = Points.Min(p => p.Y);
                double maxX = Points.Max(p => p.X);
                double maxY = Points.Max(p => p.Y);

                X = minX;
                Y = minY;
                Width = maxX - minX;
                Height = maxY - minY;
            }
            else if (ToolType == AnnotationTool.Point)
            {
                Width = 0;
                Height = 0;
            }
            // 对于矩形，X, Y, Width, Height 是直接设置的。
        }

        /// <summary>
        /// 关联的UI元素
        /// </summary>
        [JsonIgnore] // 如果你使用JSON序列化的话
        public Control? UIElement { get; set; }
    }
}
