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
        public string name;
        [ObservableProperty]
        public List<SingleCropArea> coprs = [];
    }
    public partial class SingleCropArea : ViewModelBase
    {
        [ObservableProperty]
        public string name;
        [ObservableProperty]
        public int x1;
        [ObservableProperty]
        public int y1;
        [ObservableProperty]
        public int x2;
        [ObservableProperty]
        public int y2;
    }
}
