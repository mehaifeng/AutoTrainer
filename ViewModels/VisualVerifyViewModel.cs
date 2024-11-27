using AutoTrainer.Helpers;
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
using static System.Net.WebRequestMethods;

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
        private ObservableCollection<MutationImage> mutationImages;
        [ObservableProperty]
        private int currentPage = 1;
        [ObservableProperty]
        private int totalPages = 0;

        [RelayCommand]
        public void Loaded()
        {
            LoadMutationData(CurrentPage);
        }
        [RelayCommand]
        public void Left()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                LoadMutationData(CurrentPage);
            }
        }
        [RelayCommand]
        public void Right()
        {
            if (CurrentPage < totalPages)
            {
                CurrentPage++;
                LoadMutationData(CurrentPage);
            }
        }
        /// <summary>
        /// 加载变异图像
        /// </summary>
        public void LoadMutationData(int page)
        {
            MutationImages = [];
            string dataSetPath = App.TrainModel.TrainDataPath;
            var typeClasses = Directory.GetDirectories(dataSetPath);
            if (typeClasses.Length > 0)
            {
                // App.TrainModel.NumClasses = typeClasses.Length;
                App.TrainModel.TrainDataPath = dataSetPath;
                var files = new List<string>();
                foreach (var typePath in typeClasses)
                {
                    files.AddRange(Directory.GetFiles(typePath)
                        .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".bmp")).ToList());
                }
                var image = files[1];
                Shuffle(files);

                //ImageAugmentation.AugmentImage(image, dataSetPath, 5);
                var tofiles = files.Skip((page - 1) * 500).Take(500);
                //TotalPages = (files.Count + 499) / 500;
                foreach (var file in tofiles)
                {
                    ImageAugmentation.AugmentImage(image, , 1);
                    using (var stream = System.IO.File.OpenRead(file))
                    {
                        var bitmap = new Bitmap(stream);
                        var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                        MutationImages.Add(new MutationImage
                        {
                            //className = Path.GetFileName(typePath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)),
                            thumbnail = thumbnail
                        });

                    }
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
        public static void Shuffle<T>(List<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
