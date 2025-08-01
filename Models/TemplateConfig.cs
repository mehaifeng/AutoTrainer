using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public partial class TemplateConfig:ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private List<AnnotationItem> annotations = new();

        [ObservableProperty]
        private string imageSize = string.Empty;

        [ObservableProperty]
        private DateTime createdTime = DateTime.Now;
    }
}
