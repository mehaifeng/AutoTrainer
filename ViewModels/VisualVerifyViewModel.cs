using AutoTrainer.Models;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AutoTrainer.ViewModels
{
    public class MutationImage
    {
        public string className { get; set; }
        public IImage thumbnail { get; set; }
    }
    public partial class VisualVerifyViewModel : ViewModelBase
    {
        public VisualVerifyViewModel()
        {

        }
        [ObservableProperty]
        private ObservableCollection<MutationImage> mutationImages = [];

        [RelayCommand]
        public void LoadMutationData()
        {
            string dataSetPath = App.TrainModel.TrainDataPath;
            var typeClasses = Directory.GetDirectories(dataSetPath);
            if (typeClasses.Length > 0)
            {
                // App.TrainModel.NumClasses = typeClasses.Length;
                App.TrainModel.TrainDataPath = dataSetPath;
                foreach (var typePath in typeClasses)
                {
                    var files = Directory.GetFiles(typePath)
                        .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".bmp"))
                        .ToArray(); // 可根据需求选择文件类型
                    int count = files.Length;
                    foreach (var file in files)
                    {
                        using (var stream = File.OpenRead(file))
                        {
                            var bitmap = new Bitmap(stream);
                            var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                            MutationImages.Add(new MutationImage
                            {
                                className = Path.GetFileName(typePath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)),
                                thumbnail = thumbnail
                            });
                        }
                    }
                }
                MutationImages.Shuffle();
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
    }
}
