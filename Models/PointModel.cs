using Avalonia.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class PointModel
    {
        public string AnnotationGuid { get; } = Guid.NewGuid().ToString();
        /// <summary>
        /// 点的X坐标
        /// </summary>
        public double X { get; set; }
        /// <summary>
        /// 点的Y坐标
        /// </summary>
        public double Y { get; set; }
        /// <summary>
        /// 点的类别名称
        /// </summary>
        public string ClassName { get; set; } = string.Empty;
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
