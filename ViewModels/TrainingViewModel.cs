using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class TrainingViewModel : ViewModelBase
    {
        public TrainingViewModel()
        {

        }
        [ObservableProperty]
        public ISeries[] series = [
        new ColumnSeries<int>(3, 4, 2),
        new ColumnSeries<int>(4, 2, 6),
        new ColumnSeries<double, DiamondGeometry>(4, 3, 4)
    ];
    }
}
