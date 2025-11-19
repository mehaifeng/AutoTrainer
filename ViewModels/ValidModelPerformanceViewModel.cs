using AutoTrainer.Helpers;
using AutoTrainer.Models;
using AutoTrainer.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{

    public partial class ValidModelPerformanceViewModel : ViewModelBase
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        #region 可绑定属性
        
        [ObservableProperty]
        private bool isCheckedClassifyMode = true;
        [ObservableProperty]
        private string validDatasetPath = string.Empty;
        [ObservableProperty]
        private ObservableCollection<Bitmap> validDatasetImagePreviews;
        #endregion

        public ValidModelPerformanceViewModel()
        {
            ValidDatasetImagePreviews = [];
        }
        #region 命令
        /// <summary>
        /// 选择验证集目录
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SelectValidDatasetPath()
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime destop ? destop.MainWindow as SelectTrainingTypeView : null;
            if (mainWindow != null)
            {
                var toplevel = TopLevel.GetTopLevel(mainWindow);
                if (toplevel != null)
                {
                    var folder = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                    {
                        Title = "选择验证集目录",
                        AllowMultiple = false,
                    });
                    if (folder.Count > 0)
                    {
                        ValidDatasetPath = folder[0].TryGetLocalPath()?? string.Empty;
                        await LoadValidDatasetImagePreview();
                    }
                }
            }
        }
        #endregion

        #region 函数
        /// <summary>
        /// 加载验证集预览图片
        /// </summary>
        /// <returns></returns>
        private async Task LoadValidDatasetImagePreview()
        {
            ValidDatasetImagePreviews = [];
            if (!string.IsNullOrEmpty(ValidDatasetPath))
            {
                if (Directory.Exists(ValidDatasetPath))
                {
                    var allowExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase){".jpg",".png",".jepg",".bmp",".tiff",".webp"};
                    var files = Directory.EnumerateFiles(ValidDatasetPath, "*", SearchOption.TopDirectoryOnly).Where(file => allowExtensions.Contains(Path.GetExtension(file)));
                    if (files.Any())
                    {
                        foreach (var file in files)
                        {
                            using var fileStream = File.OpenRead(file);
                            using var originalBitmap = new Bitmap(fileStream);
                            var scale = Math.Min(150 / originalBitmap.Size.Width, 150 / originalBitmap.Size.Height);
                            var newWidth = (int)(originalBitmap.Size.Width * scale);
                            var newHeight = (int)(originalBitmap.Size.Height * scale);
                            var finalBitmap = originalBitmap.CreateScaledBitmap(new Avalonia.PixelSize(newWidth,newHeight));
                            ValidDatasetImagePreviews.Add(finalBitmap);
                        }
                    }
                }
            }
        }
        #endregion
    }
}