﻿using AutoTrainer.Helpers;
using AutoTrainer.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
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
            ClassifiedImages = [];
        }
        [ObservableProperty]
        private ObservableCollection<MutationImage> mutationImages;
        [ObservableProperty]
        private ObservableCollection<PreviewImageModel> classifiedImages;
        [ObservableProperty]
        private bool isLoadingMutationData = false;
        [ObservableProperty]
        private bool isInSortingTask = false;
        [ObservableProperty]
        private Thumbnail selectImage;
        [ObservableProperty]
        private string describeImage;
        [ObservableProperty]
        private bool isShowNextPageBtn = false;

        [RelayCommand]
        public async Task Loaded(UserControl o)
        {
            IsLoadingMutationData = true;
            await Task.Run(LoadMutationData);
            IsLoadingMutationData = false;
        }
        [RelayCommand]
        public async Task Classify()
        {
            ClassifiedImages = [];
            var todayFolder = Path.Combine(App.PyClassifyLogFolderPath, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(todayFolder);
            var specialPyLogPath = Path.Combine(todayFolder, $"Predicted_{DateTime.Now.ToString("HHmmss")}.json");
            var classifyPyFilePath = Path.Combine(Environment.CurrentDirectory, "PyScripts/ImageClassifier.py");
            var modelPath = Path.Combine(App.ModelOutputFolderPath, App.TrainModel.PretrainedModel + ".pth");
            StringBuilder sb = new StringBuilder();
            sb.Append($"{App.PythonVenvPath}\\Scripts\\activate.bat && ");
            sb.Append($" python {classifyPyFilePath}");
            sb.Append($" --image-folder {App.MutationDataPath}");
            sb.Append($" --model-path {modelPath}");
            sb.Append($" --model-name {App.TrainModel.PretrainedModel}");
            sb.Append($" --image-folder {App.MutationDataPath}");
            sb.Append($" --num-classes {App.TrainModel.NumClasses}");
            sb.Append($" --output-path {specialPyLogPath}");
            sb.Append($" && {App.PythonVenvPath}\\Scripts\\deactivate.bat");
            IsInSortingTask = true;
            await CmdHelper.ExecuteCmdWindow(sb.ToString(), false);
            if (System.IO.File.Exists(specialPyLogPath))
            {
                var jsonStr = await System.IO.File.ReadAllTextAsync(specialPyLogPath);
                var classifyResult = JsonConvert.DeserializeObject<List<ClassifyModel>>(jsonStr);
                if (classifyResult != null)
                {
                    var imageGroup = classifyResult.GroupBy(x => x.predictedClass);
                    foreach (var thisGroup in imageGroup)
                    {
                        List<Thumbnail> thumbnails = [];
                        foreach (var result in thisGroup)
                        {
                            if (result.imagePath == null) continue;
                            using (var stream = System.IO.File.OpenRead(result.imagePath))
                            {
                                var actualClass = result.imagePath.Split("_CLASS_")[1];
                                var bitmap = new Bitmap(stream);
                                var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                                thumbnails.Add(new Thumbnail
                                {
                                    Image = thumbnail,
                                    ImagePath = result.imagePath,
                                    ActualClass = actualClass ?? "NULL",
                                    PredictClass = result.predictedClass ?? "NULL",
                                    Confidence = result.confidence,
                                });
                            }
                        }
                        ClassifiedImages.Add(new PreviewImageModel
                        {
                            ClassName = thisGroup.First().predictedClass,
                            Thumbnails = new ObservableCollection<Thumbnail>(thumbnails)
                        });
                    }
                }
            }
            IsInSortingTask = false;
            IsShowNextPageBtn = true;
        }
        [RelayCommand]
        private static async Task GoToExportPage(UserControl o)
        {
            if (o.Parent != null && o.Parent.Parent is TabControl control)
            {
                await Dispatcher.UIThread.InvokeAsync(() => control.SelectedIndex = 5);
            }
        }


        #region 函数
        /// <summary>
        /// 加载变异图像
        /// </summary>
        public void LoadMutationData()
        {
            MutationImages = [];
            string dataSetPath = App.TrainModel.TrainDataPath;
            if (dataSetPath == null)
            {
                return;
            }
            else
            {
                var typeClasses = Directory.GetDirectories(dataSetPath);

                if (typeClasses.Length > 0)
                {
                    // App.TrainModel.NumClasses = typeClasses.Length;
                    App.TrainModel.TrainDataPath = dataSetPath;
                    var files = new List<string>();
                    Dictionary<string,int> classNameToIndexDic = [];
                    for (int i = 0;i< typeClasses.Length;i++)
                    {
                        classNameToIndexDic.Add(typeClasses[i].Split("\\").Last(),i);
                        files.AddRange(Directory.GetFiles(typeClasses[i])
                            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".bmp")).ToList());
                    }
                    Shuffle(files);
                    //ImageAugmentation.AugmentImage(image, dataSetPath, 5);
                    var tofiles = files.Take(200).ToArray();
                    var mutationDatas = Directory.GetFiles(App.MutationDataPath);
                    foreach (var readyToDelete in mutationDatas)
                    {
                        System.IO.File.Delete(readyToDelete);
                    }
                    for(int i=0;i<tofiles.Length;i++)
                    {
                        var count = tofiles[i].Split("\\");
                        var className = count[count.Length - 2];
                        var index = classNameToIndexDic[className];
                        ImageAugmentation.AugmentImage(index, tofiles[i], App.MutationDataPath, 1);
                    }
                    mutationDatas = Directory.GetFiles(App.MutationDataPath);
                    foreach (var mutationData in mutationDatas)
                    {
                        using (var stream = System.IO.File.OpenRead(mutationData))
                        {
                            var bitmap = new Bitmap(stream);
                            var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                            MutationImages.Add(new MutationImage
                            {
                                //className = mutationData.Split("_CLASS_")[1],
                                thumbnail = thumbnail
                            });
                        }
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
        #endregion
    }
}
