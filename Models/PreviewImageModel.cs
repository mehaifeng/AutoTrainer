using AutoTrainer.ViewModels;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public partial class PreviewImageModel : ViewModelBase
    {
        [ObservableProperty]
        private string? className;
        [ObservableProperty]
        private ObservableCollection<Thumbnail>? thumbnails = [];
    }
    public partial class Thumbnail : ViewModelBase
    {
        [ObservableProperty]
        private IImage? image;
        [ObservableProperty]
        private string? imagePath;
        [ObservableProperty]
        private string? actualClass;
        [ObservableProperty]
        private string? predictClass;
        [ObservableProperty]
        private double confidence;

    }
}
