using Avalonia.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class RectangleModel
    {   
        public string AnnotationGuid { get; } = Guid.NewGuid().ToString();
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
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
