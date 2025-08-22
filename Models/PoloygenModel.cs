using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class PoloygenModel
    {
        public string AnnotationGuid { get; } = Guid.NewGuid().ToString();
        public string ClassName { get; set; } = string.Empty;
        /// <summary>
        /// 多边形标注,存储多边形的顶点集合。
        /// </summary>
        public ObservableCollection<Avalonia.Point> Points = new ObservableCollection<Avalonia.Point>();
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;
        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected { get; set; } = false;
        /// <summary>
        /// 关联的UI元素
        /// </summary>
        [JsonIgnore]
        public Control? UIElement { get; set; }
    }
}
