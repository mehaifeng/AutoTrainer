using AutoTrainer.Emuns;
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
    public abstract partial class AnnotationItem : ObservableObject
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        [ObservableProperty]
        private string? className;

        /// <summary>
        /// 显示颜色
        /// </summary>
        [ObservableProperty]
        [property:JsonIgnore]
        private Avalonia.Media.Color displayColor = Avalonia.Media.Colors.Red;

        /// <summary>
        /// 是否被选中
        /// </summary>
        [JsonIgnore]
        public bool IsSelected { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        [JsonIgnore]
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 标注框类型
        /// </summary>
        public AnnotationToolEnum AnnotationType { get; set; }

        /// <summary>
        /// 实例唯一标识
        /// </summary>
        public string InstanceGuid { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 抽象方法：获取边界框
        /// </summary>
        public abstract BoundingBoxModel GetBoundingBox();

        [ObservableProperty]
        [property:JsonIgnore]
        private BoundingBoxModel? boundingBoxInfo;

        public void UpdateBoundingBoxInfo()
        {
            BoundingBoxInfo = GetBoundingBox();
        }

        /// <summary>
        /// 虚方法：判断某点是否在标注区域内
        /// </summary>
        /// <param name="x">点的X坐标</param>
        /// <param name="y">点的Y坐标</param>
        /// <returns>是否包含该点</returns>
        public virtual bool Contains(double x, double y)
        {
            var boundingBox = GetBoundingBox();
            return x >= boundingBox.X && x <= boundingBox.X + boundingBox.Width && y >= boundingBox.Y && y <= boundingBox.Y + boundingBox.Height;
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
