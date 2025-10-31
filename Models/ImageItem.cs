using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public partial class ImageItem:ObservableObject
    {
        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private Bitmap? thumbnail;

        [ObservableProperty]
        private bool isSelected = false;

        [ObservableProperty]
        private bool isAnnotated = false;

        [ObservableProperty]
        private int annotationCount = 0;

        [ObservableProperty]
        private bool asCroppingTemplate = false;

        public ObservableCollection<string> ImageClasses { get; } = new();

    }
}
