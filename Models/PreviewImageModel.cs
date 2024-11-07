using AutoTrainer.ViewModels;
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
    public partial class PreviewImageModel : ViewModelBase
    {
        [ObservableProperty]
        private string? name;
        [ObservableProperty]
        private ObservableCollection<Bitmap> thumbnails = [];
    }
}
