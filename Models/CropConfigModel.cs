using AutoTrainer.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public partial class CropConfigModel : ViewModelBase
    {
        [ObservableProperty]
        private string name;
        [ObservableProperty]
        private List<SingleCropArea> coprs = [];
    }
    public partial class SingleCropArea : ViewModelBase
    {
        [ObservableProperty]
        private string name;
        [ObservableProperty]
        private int x1;
        [ObservableProperty]
        private int y1;
        [ObservableProperty]
        private int x2;
        [ObservableProperty]
        private int y2;
    }
}
