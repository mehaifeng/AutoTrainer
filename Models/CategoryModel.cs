using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 类别模型，包含类别名称和颜色
    /// </summary>
    public partial class CategoryModel : ObservableObject
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        [ObservableProperty]
        private string name = string.Empty;

        /// <summary>
        /// 类别颜色
        /// </summary>
        [ObservableProperty]
        private Color color = Colors.Gray;

        public CategoryModel() { }

        public CategoryModel(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
