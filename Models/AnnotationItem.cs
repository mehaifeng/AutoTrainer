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
    public abstract partial class AnnotationItem
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        public string? ClassName { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 类别唯一标识
        /// </summary>
        public AnnotationToolEnum AnnotationType { get; set; }

        /// <summary>
        /// 实例唯一标识
        /// </summary>
        public string InstanceGuid { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 抽象方法：获取边界框
        /// </summary>
        public abstract (double MinX, double MinY, double MaxX, double MaxY) GetBoundingBox();

        /// <summary>
        /// 虚方法：判断某点是否在标注区域内
        /// </summary>
        /// <param name="x">点的X坐标</param>
        /// <param name="y">点的Y坐标</param>
        /// <returns>是否包含该点</returns>
        public virtual bool Contains(double x, double y)
        {
            var (minX, minY, maxX, maxY) = GetBoundingBox();
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        /// <summary>
        /// 克隆当前标注对象
        /// </summary>
        public abstract AnnotationItem Clone();

        /// <summary>
        /// 标注对象的UI元素
        /// </summary>
        [JsonIgnore]
        public Control? UIElement { get; set; }
    }
}
