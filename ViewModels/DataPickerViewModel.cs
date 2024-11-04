using AutoTrainer.ControlHelper;
using AutoTrainer.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class DataPickerViewModel : ViewModelBase
    {
        private int RectCounter { get; set; }
        private List<CorpConfigModel> CorpConfigs { get; set; }
        private int ConfigCount { get; set; } = 0;
        public CorpConfigModel CorpConfig { get; set; }
        public DataPickerViewModel()
        {
            CorpConfigs = [];
        }
        #region
        [ObservableProperty]
        private IImage imagepath;
        [ObservableProperty]
        private string cropConfigTitle;
        [ObservableProperty]
        private bool isEnableLeftBtn;
        [ObservableProperty]
        private bool isEnableRightBtn;
        #endregion

        #region 命令
        /// <summary>
        /// 添加裁剪配置
        /// </summary>
        [RelayCommand]
        public async Task AddCroppingConfig(Canvas canvas)
        {
            var thisWindow = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var toplevel = TopLevel.GetTopLevel(thisWindow?.MainWindow);
            if (toplevel != null)
            {
                var file = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = "Select a Image",
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.ImageAll],
                });
                if (file.Count == 1)
                {
                    Imagepath = new Bitmap(file[0].Path.AbsolutePath);
                    //先清除方框
                    canvas.Children.Clear();
                    if (ConfigCount > 0)
                    {
                        CorpConfigs.Add(new CorpConfigModel()
                        {
                            Name = CorpConfig.Name,
                            Coprs = CorpConfig.Coprs,
                        });
                    }
                    CorpConfig = new CorpConfigModel()
                    {
                        Name = "Config" + ConfigCount,
                    };
                    ConfigCount++;
                    CropConfigTitle = CorpConfig.Name;
                }
            }
        }
        /// <summary>
        /// 添加方框
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        public void AddCropRectangle(Canvas canvas)
        {
            var newArea = new SingleCorpArea
            {
                Name = $"Area{RectCounter++}",
                X1 = 50,
                Y1 = 50,
                X2 = 150,
                Y2 = 150
            };
            CorpConfig.Coprs.Add(newArea);
            DraggableRectangle.AddDraggableRectangle(newArea, canvas);
        }
        [RelayCommand]
        public void Left()
        {

        }
        [RelayCommand]
        public void Right()
        {

        }
        #endregion
    }
}