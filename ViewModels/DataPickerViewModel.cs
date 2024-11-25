using AutoTrainer.ControlHelper;
using AutoTrainer.Helpers;
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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class DataPickerViewModel : ViewModelBase
    {
        private readonly string appPath = AppDomain.CurrentDomain.BaseDirectory;
        private int ConfigCount { get; set; } = 0;
        private CancellationTokenSource cts = new();
        public DataPickerViewModel()
        {
            CropConfigs = [];
            ImageCategories = [];
            LoadConfig();
        }
        #region
        [ObservableProperty]
        private ObservableCollection<CropConfigModel> cropConfigs;
        [ObservableProperty]
        public ObservableCollection<PreviewImageModel> imageCategories;
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
        [ObservableProperty]
        private string cropOutputPath;
        [ObservableProperty]
        private int progressValue;
        [ObservableProperty]
        private int progressMax;
        [ObservableProperty]
        private string progressState = "0/0";
        [ObservableProperty]
        private bool isEnableEndBtn = false;
        [ObservableProperty]
        private bool isVisibleFuncArea = false;
        [ObservableProperty]
        private string previewState;
        [ObservableProperty]
        private bool isVisibleIntroduce = false;
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
                    Imagepath = new Bitmap(file[0].Path.LocalPath);
                    //先清除方框
                    canvas.Children.Clear();
                    //保存之前的配置
                    if (CropConfigs.Count > 0)
                    {
                        await SaveConfig();
                    }
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
                    Title = "选择一张图片",
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.ImageAll],
                });
                if (file.Count == 1)
                {
                    Imagepath = new Bitmap(file[0].Path.LocalPath);
                    if (CropConfigs != null && CropConfigs.Count > 1)
                    {
                        if (CropConfig != CropConfigs.Last())
                        {
                            IsEnableRightBtn = true;
                        }
                    }
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
            if (index == -1)
                return;

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
        /// <summary>
        /// 快速裁剪
        /// </summary>
        [RelayCommand]
        private async Task QuickCrop()
        {
            if (CropConfig != null)
            {
                try
                {
                    //先保存
                    if (CropConfigs.Count > 0)
                    {
                        App.TrainModel.TrainDataPath = CropOutputPath;
                        await SaveConfig();
                    }
                    await Task.Run(() => CropImage(cts.Token));
                    LoadPreviewImage(CropOutputPath);
                }
                catch (Exception ex)
                {
                    var messagebox = MessageBoxManager.GetMessageBoxStandard("Caption", $"{ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.YesNo);
                    await messagebox.ShowAsync();
                }
            }
        }
        /// <summary>
        /// 中止裁剪
        /// </summary>
        [RelayCommand]
        private void End()
        {
            cts.Cancel();
            cts = new CancellationTokenSource();
            IsEnableEndBtn = false;
            IsVisibleFuncArea = false;
            //LoadPreviewImage("D:\\VsSources\\AutoTrainer\\bin\\Debug\\net8.0\\CropImages\\1");
        }
        /// <summary>
        /// 预览图像
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task PreviewImageAsync()
        {
            var thisWindow = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var toplevel = TopLevel.GetTopLevel(thisWindow?.MainWindow);
            if (toplevel != null)
            {
                var folder = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = "选择文件夹",
                    AllowMultiple = false,
                });
                if (folder.Count == 1)
                {
                    LoadPreviewImage(folder[0].Path.LocalPath);
                }
            }
        }
        /// <summary>
        /// 切换图像预览Tab页
        /// </summary>
        /// <param name="selectItem"></param>
        [RelayCommand]
        private void TabChanged(PreviewImageModel selectItem)
        {

        }
        /// <summary>
        /// 转到下一页
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [RelayCommand]
        private async Task GoToNextTab(UserControl o)
        {
            if (o != null && o.Parent != null)
            {
                if (o.Parent.Parent is TabControl control)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        control.SelectedIndex = 2;
                    });
                }
            }
        }
        #endregion

        #region 函数
        /// <summary>
        /// 裁剪图片
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CropImage(CancellationToken token)
        {
            var thisWindow = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var toplevel = TopLevel.GetTopLevel(thisWindow?.MainWindow);
            CropOutputPath = System.IO.Path.Combine(appPath, "CropImages");
            if (toplevel != null)
            {
                var folders = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = "选择文件夹",
                    AllowMultiple = true,
                });
                if (folders != null)
                {
                    ProgressState = "0/0";//开始时，进度设置为0/0
                    IsEnableEndBtn = true;//中止按钮设置为可见
                    IsVisibleFuncArea = true;
                    List<string> files = [];
                    foreach (var folder in folders)
                    {
                        files.AddRange(Directory.GetFiles(folder.Path.AbsolutePath));
                    }
                    ProgressMax = files.Count;//进度条最大值设置为文件数目
                    for (int i = 0; i < files.Count; i++)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            int index = files[i].LastIndexOf('.');
                            string extents = files[i].Substring(index);
                            if (extents == ".jpeg" || extents == ".jpg" || extents == ".png" || extents == ".bmp")
                            {
                                CropImageHelper.CropImage(files[i], CropOutputPath, CropConfig);
                                ProgressValue = i + 1;
                                ProgressState = $"{i + 1}/{ProgressMax}";
                            }
                        }
                    }
                    IsVisibleFuncArea = false;
                    IsEnableEndBtn = false;
                }
            }
        }
        /// <summary>
        /// 预览图片
        /// </summary>
        /// <param name="folderPath"></param>
        private void LoadPreviewImage(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var typeClasses = Directory.GetDirectories(folderPath);
                if (typeClasses.Length > 0)
                {
                    // App.TrainModel.NumClasses = typeClasses.Length;
                    App.TrainModel.TrainDataPath = folderPath;
                    foreach (var typePath in typeClasses)
                    {
                        var files = Directory.GetFiles(typePath)
                            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".bmp"))
                            .ToArray(); // 可根据需求选择文件类型
                        int count = files.Length;
                        var toFiles = files.Take(20);
                        PreviewImageModel model = new()
                        {
                            Name = System.IO.Path.GetFileName(typePath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
                        };
                        foreach (var file in toFiles)
                        {
                            using (var stream = File.OpenRead(file))
                            {
                                var bitmap = new Bitmap(stream);
                                var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                                model.Thumbnails.Add(thumbnail);
                            }
                        }
                        PreviewState += $"{model.Name}:{count}张; ";
                        ImageCategories.Add(model);
                    }
                    IsVisibleIntroduce = true;
                }
            }
        }
        /// <summary>
        /// 缩放图片
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private Bitmap ResizeBitmap(Bitmap bitmap, int width, int height)
        {
            // 缩放图片
            var resizedBitmap = bitmap.CreateScaledBitmap(new PixelSize(width, height), BitmapInterpolationMode.MediumQuality);
            return resizedBitmap;
        }
        /// <summary>
        /// 加载配置
        /// </summary>
        private async void LoadConfig()
        {
            string configPath = System.IO.Path.Combine(App.ConfigFolderPath, "CropConfig.json");
            if (File.Exists(configPath))
            {
                string jsonStr = await File.ReadAllTextAsync(configPath);
                CropConfigs = JsonConvert.DeserializeObject<ObservableCollection<CropConfigModel>>(jsonStr);
                ConfigCount = CropConfigs.Count;
                CropConfig = CropConfigs.First();
            }
        }
        /// <summary>
        /// 保存配置
        /// </summary>
        private async Task SaveConfig()
        {
            string jsonStr = JsonConvert.SerializeObject(CropConfigs);
            var configPath = System.IO.Path.Combine(App.ConfigFolderPath, "CropConfig.json");
            await File.WriteAllTextAsync(configPath, jsonStr);
        }
        #endregion
    }
}