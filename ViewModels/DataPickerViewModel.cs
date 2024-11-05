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
        private List<CropConfigModel> CropConfigs { get; set; }
        private int ConfigCount { get; set; } = 0;
        public CropConfigModel CropConfig { get; set; }
        public DataPickerViewModel()
        {
            CropConfigs = [];
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
                    CropConfig = new CropConfigModel()
                    {
                        Name = "Config" + ConfigCount,
                    };
                    CropConfigs.Add(new CropConfigModel()
                    {
                        Name = CropConfig.Name,
                        Coprs = CropConfig.Coprs,
                    });
                    ConfigCount++;
                    IsEnableLeftBtn = true;
                    CropConfigTitle = CropConfig.Name;
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
            var newArea = new SingleCropArea
            {
                Name = $"Area{RectCounter++}",
                X1 = 50,
                Y1 = 50,
                X2 = 150,
                Y2 = 150
            };
            CropConfig.Coprs.Add(newArea);
            DraggableRectangle.AddDraggableRectangle(newArea, canvas);
        }
        /// <summary>
        /// 选择图片
        /// </summary>
        [RelayCommand]
        public async Task SelectPicture()
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
                }
            }
        }
        /// <summary>
        /// 向左
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        public void Left(Canvas canvas)
        {
            if (CropConfigs == null || CropConfigs.Count == 0)
                return;

            var currentIndex = CropConfigs.FindIndex(t => string.Equals(t.Name, CropConfigTitle));
            if (currentIndex == -1)
                return;

            var previewIndex = currentIndex - 1;
            if (previewIndex < 0)
                return;

            foreach (var crop in CropConfigs)
            {
                if (string.Equals(crop.Name, CropConfigTitle))
                {
                    crop.Coprs = CropConfig.Coprs;
                }
            }
            // 更新标题
            CropConfigTitle = CropConfigs[previewIndex].Name;
            canvas.Children.Clear();
            CropConfig = new CropConfigModel();
            foreach (var crop in CropConfigs[previewIndex].Coprs)
            {
                var newArea = new SingleCropArea
                {
                    Name = crop.Name,
                    X1 = crop.X1,
                    Y1 = crop.Y1,
                    X2 = crop.X2,
                    Y2 = crop.Y2
                };
                CropConfig.Coprs.Add(crop);
                DraggableRectangle.AddDraggableRectangle(newArea, canvas);
            }
            // 更新按钮状态
            IsEnableRightBtn = true; // 既然能往左,说明右边一定有元素
            IsEnableLeftBtn = previewIndex > 0; // 检查是否还能继续往左
        }
        /// <summary>
        /// 向右
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        public void Right(Canvas canvas)
        {
            if (CropConfigs == null || CropConfigs.Count == 0)
                return;

            var currentIndex = CropConfigs.FindIndex(t => string.Equals(t.Name, CropConfigTitle));
            if (currentIndex == -1)
                return;

            var nextIndex = currentIndex + 1;
            if (nextIndex >= CropConfigs.Count)
                return;

            foreach (var crop in CropConfigs)
            {
                if (string.Equals(crop.Name, CropConfigTitle))
                {
                    crop.Coprs = CropConfig.Coprs;
                }
            }
            // 更新标题
            CropConfigTitle = CropConfigs[nextIndex].Name;
            canvas.Children.Clear();
            CropConfig = new CropConfigModel();
            foreach (var crop in CropConfigs[nextIndex].Coprs)
            {
                var newArea = new SingleCropArea
                {
                    Name = crop.Name,
                    X1 = crop.X1,
                    Y1 = crop.Y1,
                    X2 = crop.X2,
                    Y2 = crop.Y2
                };
                CropConfig.Coprs.Add(crop);
                DraggableRectangle.AddDraggableRectangle(newArea, canvas);
            }
            // 更新按钮状态
            IsEnableLeftBtn = true; // 既然能往右,说明左边一定有元素
            IsEnableRightBtn = nextIndex < CropConfigs.Count - 1; // 检查是否还能继续往右
        }
        #endregion
    }
}