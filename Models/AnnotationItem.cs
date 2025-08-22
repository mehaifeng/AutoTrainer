using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;

namespace AutoTrainer.Models
{
    public partial class AnnotationItem:ObservableObject
    {
        [JsonProperty(nameof(ImageName))]
        public required string ImageName { get; set; }
        [JsonProperty("RectangleAnnotation")]
        public required RectangleModel RectangleModel { get; set; }
        [JsonProperty("PoloygenAnnotation")]
        public required PoloygenModel PoloygenModel { get; set; }
        [JsonProperty("PointAnnotation")]
        public required PointModel PointModel { get; set; }
        /// <summary>
        /// 当前标注的类型
        /// </summary>
        [JsonIgnore]
        public AnnotationToolEnum ToolType { get; set; }
        [JsonIgnore]
        public string ClassName
        {
            get => ToolType switch
            {
                AnnotationToolEnum.Rectangle => RectangleModel.ClassName,
                AnnotationToolEnum.Polygon => PoloygenModel.ClassName,
                AnnotationToolEnum.Point => PointModel.ClassName,
                _ => string.Empty
            };
            set
            {
                if (ToolType == AnnotationToolEnum.Rectangle)
                    RectangleModel.ClassName = value;
                else if (ToolType == AnnotationToolEnum.Polygon)
                    PoloygenModel.ClassName = value;
                else if (ToolType == AnnotationToolEnum.Point)
                    PointModel.ClassName = value;
            }
        }

        #region 边界框
        [ObservableProperty]
        [property:JsonIgnore]
        private double x;
        [ObservableProperty]
        [property: JsonIgnore]
        private double y;
        [ObservableProperty]
        [property: JsonIgnore]
        private double width;
        [ObservableProperty]
        [property: JsonIgnore]
        private double height;
        #endregion

        /// <summary>
        /// 获取标注的边界信息
        /// </summary>
        [JsonIgnore]
        public string BoundsInfo => $"({X:F0}, {Y:F0}) - {Width:F0}x{Height:F0}";

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
            RectangleModel = new RectangleModel()
            {
                ClassName = className,
                X = x,
                Y = y,
                Width = width,
                Height = height,
            };
            ToolType = AnnotationToolEnum.Rectangle;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 创建点标注。
        /// </summary>
        /// <param name="x">点的X坐标。</param>
        /// <param name="y">点的Y坐标。</param>
        /// <param name="className">标注的类别名称。</param>
        public AnnotationItem(double x, double y, string className)
        {
            PointModel = new PointModel()
            {
                X = x,
                Y = y,
                ClassName = className,
            };
            ToolType = AnnotationToolEnum.Point;
            X = x;
            Y = y;
            Width = 0;
            Height = 0;
        }

        /// <summary>
        /// 创建多边形标注。
        /// </summary>
        /// <param name="polygonPoints">多边形的顶点集合。</param>
        /// <param name="className">标注的类别名称。</param>
        public AnnotationItem(IEnumerable<Avalonia.Point> polygonPoints, string className)
        {
            PoloygenModel = new PoloygenModel()
            {
                ClassName = className,
                Points = [..polygonPoints]
            };
            ToolType = AnnotationToolEnum.Polygon;
            UpdateBoundingBox(); // 根据多边形顶点计算其边界框
        }

        /// <summary>
        /// 根据多边形的顶点更新其边界框
        /// </summary>
        public void UpdateBoundingBox()
        {
            if (ToolType == AnnotationToolEnum.Polygon && PoloygenModel.Points != null && PoloygenModel.Points.Any())
            {
                double minX = PoloygenModel.Points.Min(p => p.X);
                double minY = PoloygenModel.Points.Min(p => p.Y);
                double maxX = PoloygenModel.Points.Max(p => p.X);
                double maxY = PoloygenModel.Points.Max(p => p.Y);

                X = minX;
                Y = minY;
                Width = maxX - minX;
                Height = maxY - minY;
            }
            else if (ToolType == AnnotationToolEnum.Point)
            {
                Width = 0;
                Height = 0;
            }
        }

        #region Json控制
        public bool ShouldSerializeRectangleModel()
        {
            return ToolType == AnnotationToolEnum.Rectangle && RectangleModel != null && PoloygenModel == null && PointModel == null;
        }
        public bool ShouldSerializePolygonModel()
        {
            return ToolType == AnnotationToolEnum.Polygon && PoloygenModel != null && RectangleModel == null && PointModel == null;
        }
        public bool ShouldSerializePointModel()
        {
            return ToolType == AnnotationToolEnum.Point && PointModel != null && RectangleModel == null && PoloygenModel == null;
        }
        #endregion

    }
}
