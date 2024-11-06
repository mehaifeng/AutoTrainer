using AutoTrainer.ControlHelper;
using AutoTrainer.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class DataPickerViewModel : ViewModelBase
    {
        private int ConfigCount { get; set; } = 0;

        public DataPickerViewModel()
        {
            CropConfigs = [];
        }
        #region
        [ObservableProperty]
        private ObservableCollection<CropConfigModel> cropConfigs;
        [ObservableProperty]
        private CropConfigModel cropConfig;
        [ObservableProperty]
        private IImage imagepath;
        [ObservableProperty]
        private bool isEnableLeftBtn;
        [ObservableProperty]
        private bool isEnableRightBtn;
        [ObservableProperty]
        private bool isEnablePlusRectBtn = false;
        [ObservableProperty]
        private bool isEnableDeleteBtn = false;
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
                    IsEnablePlusRectBtn = true;
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
                    IsEnableDeleteBtn = true;
                    if (CropConfigs.Count > 1)
                    {
                        IsEnableLeftBtn = true;
                    }
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
                Name = "Class",
                X1 = 100,
                Y1 = 100,
                X2 = 270,
                Y2 = 270
            };
            CropConfig.Coprs.Add(newArea);
            var displayImage = (canvas.Parent as Grid).Children[0] as Image;
            DraggableRectangle.AddDraggableRectangle(newArea, canvas, Imagepath as Bitmap, displayImage, "Class");
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

            var currentIndex = CropConfigs.IndexOf(CropConfigs.First(t => string.Equals(t.Name, CropConfig.Name)));
            if (currentIndex == -1)
                return;

            var previewIndex = currentIndex - 1;
            if (previewIndex < 0)
                return;

            canvas.Children.Clear();
            CropConfig = CropConfigs[previewIndex];

            foreach (var crop in CropConfig.Coprs)
            {
                var displayImage = (canvas.Parent as Grid).Children[0] as Image;
                DraggableRectangle.AddDraggableRectangle(crop, canvas, Imagepath as Bitmap, displayImage, crop.Name);
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

            var currentIndex = CropConfigs.IndexOf(CropConfigs.First(t => string.Equals(t.Name, CropConfig.Name)));
            if (currentIndex == -1)
                return;

            var nextIndex = currentIndex + 1;
            if (nextIndex >= CropConfigs.Count)
                return;

            canvas.Children.Clear();
            CropConfig = CropConfigs[nextIndex];

            foreach (var crop in CropConfig.Coprs)
            {
                var displayImage = (canvas.Parent as Grid).Children[0] as Image;
                DraggableRectangle.AddDraggableRectangle(crop, canvas, Imagepath as Bitmap, displayImage, crop.Name);
            }

            // 更新按钮状态
            IsEnableLeftBtn = true; // 既然能往右,说明左边一定有元素
            IsEnableRightBtn = nextIndex < CropConfigs.Count - 1; // 检查是否还能继续往右
        }
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        private void Delete(Canvas canvas)
        {
            if (string.IsNullOrEmpty(CropConfig.Name) || CropConfigs.Count == 0)
                return;

            var index = CropConfigs.IndexOf(CropConfigs.First(t => string.Equals(t.Name, CropConfig.Name)));
            if (index == -1) return;

            if (CropConfigs.Count > 1)
            {
                if (index != CropConfigs.Count - 1)
                    Right(canvas);
                else
                    Left(canvas);
            }
            else
            {
                // 清空配置和状态
                CropConfig = new CropConfigModel();
                canvas.Children.Clear();
                Imagepath = null;
                IsEnableLeftBtn = false;
                IsEnableDeleteBtn = false;
            }

            CropConfigs.RemoveAt(index);
        }
        #endregion
    }
}