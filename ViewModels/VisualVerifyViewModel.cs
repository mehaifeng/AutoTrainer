using AutoTrainer.Helpers;
using AutoTrainer.Models;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static SkiaSharp.HarfBuzz.SKShaper;
using static System.Net.WebRequestMethods;

namespace AutoTrainer.ViewModels
{
    public class MutationImage
    {
        public string ClassName { get; set; }
        public string ImagePath { get; set; }
        public IImage Thumbnail { get; set; }
    }
    public partial class VisualVerifyViewModel : ViewModelBase
    {
        public VisualVerifyViewModel()
        {
            MutationImages = [];
            ClassifiedImages = [new PreviewImageModel() { ClassName = "类别"}];
            ModelSingleQualityTable = [new ModelSingleQuality() { ClassName = "0", Accuracy = 0, F1_score = 0, Precision = 0, Recall=0}];
            ModelMacroQualityTable = [new ModelMacroQuality() { Accuracy = 0, MacroPrecision = 0, MacroRecall = 0, MacroF1 = 0 }];
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
        private string predictClass;
        [ObservableProperty]
        private string confidence;
        [ObservableProperty]
        private string actualClass;
        [ObservableProperty]
        private string imagePath;
        [ObservableProperty]
        private bool isShowNextPageBtn = false;
        [ObservableProperty]
        private ObservableCollection<ModelSingleQuality> modelSingleQualityTable;
        [ObservableProperty]
        private ObservableCollection<ModelMacroQuality> modelMacroQualityTable;
        [ObservableProperty]
        private bool isSpinning = false;
        [ObservableProperty]
        private int validationImageRate = 20;

        /// <summary>
        /// 第一次打开Tab
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [RelayCommand]
        public async Task Loaded(UserControl o)
        {
            if (MutationImages.Count == 0)
            {
                IsSpinning = true;
                IsLoadingMutationData = true;
                await Task.Run(LoadMutationData);
                IsLoadingMutationData = false;
                IsSpinning = false;
            }
        }
        /// <summary>
        /// 分类
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task Classify(Grid o)
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
                    CalculateModelPerformance(classifyResult,o);
                }
            }
            IsInSortingTask = false;
            IsShowNextPageBtn = true;
        }
        /// <summary>
        /// 转到下一页
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [RelayCommand]
        private static async Task GoToExportPage(UserControl o)
        {
            if (o.Parent != null && o.Parent.Parent is TabControl control)
            {
                await Dispatcher.UIThread.InvokeAsync(() => control.SelectedIndex = 5);
            }
        }
        /// <summary>
        /// 刷新变异数据集
        /// </summary>
        [RelayCommand]
        private async Task RefreshMutation()
        {
            if(!IsInSortingTask && !IsLoadingMutationData && !IsSpinning)
            {
                IsSpinning = true;
                IsLoadingMutationData = true;
                await Task.Run(LoadMutationData);
                IsLoadingMutationData = false;
                IsSpinning = false;
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
                    //一共要获取的文件数目
                    var getFilesCount = Convert.ToInt32(files.Count * ValidationImageRate * 0.01);
                    var tofiles = files.Take(getFilesCount).ToArray();
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
                        ImageAugmentation.AugmentImageOne(index, tofiles[i], App.MutationDataPath, 1);
                    }
                    mutationDatas = Directory.GetFiles(App.MutationDataPath);
                    var pattern = @"^(.*)\(";
                    foreach (var mutationData in mutationDatas)
                    {
                        using (var stream = System.IO.File.OpenRead(mutationData))
                        {
                            var bitmap = new Bitmap(stream);
                            var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                            var classDescribe = mutationData.Split("_CLASS_")[1];
                            Match match = Regex.Match(classDescribe, pattern);
                            MutationImages.Add(new MutationImage
                            {
                                ClassName = match.Groups[1].Value,
                                ImagePath = mutationData,
                                Thumbnail = thumbnail
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
        /// <summary>
        /// 统计模型的性能
        /// </summary>
        private void CalculateModelPerformance(List<ClassifyModel> results, Grid o)
        {
            ModelSingleQualityTable = [];
            ModelMacroQualityTable = [];
            List<(string, string)> valueKeys = [];
            foreach (var result in results)
            {
                var predicted = result.predictedClass;
                var actual = MutationImages.First(t => t.ImagePath == result.imagePath).ClassName;
                valueKeys.Add((actual, predicted));
            }
            ModelMacroQuality modelMacroQuality = new();
            var groups = valueKeys.GroupBy(x => x.Item1).ToArray();
            int totalCorrectCount = 0;
            foreach (var group in groups)
            {
                int thisGroupCorrectCount = 0;
                ModelSingleQuality modelSingleQuality = new();
                foreach (var key in group)
                {
                    //actual,predict
                    if(key.Item1 == key.Item2)
                    {
                        thisGroupCorrectCount++;
                        totalCorrectCount++;
                    }
                }
                //该类的精确率
                double precision = (double)thisGroupCorrectCount / (double)valueKeys.Count(t => t.Item2 == group.Key);
                //该类的召回率
                double recall = (double)thisGroupCorrectCount / (double)group.Count();
                //该类的F1分数(精确率和召回率的调和平均值)
                double f1Score = 2 * (precision * recall)/(precision + recall);
                modelSingleQuality.Precision = Math.Round(precision,3);
                modelSingleQuality.Recall = Math.Round(recall,3);
                modelSingleQuality.F1_score = Math.Round(f1Score,3);
                modelSingleQuality.ClassName = group?.Key;
                ModelSingleQualityTable.Add(modelSingleQuality);
            }
            //整体准确率
            double accuracy = (double)totalCorrectCount / (double)results.Count;
            //宏精确率
            double marcoPrecision = Enumerable.Average(ModelSingleQualityTable.Select(T => T.Precision));
            //宏召回率
            double macrorecall = Enumerable.Average(ModelSingleQualityTable.Select(T => T.Recall));
            //宏F1值
            double macroF1 = Enumerable.Average(ModelSingleQualityTable.Select(T => T.F1_score));
            modelMacroQuality.MacroF1 = Math.Round(macroF1,3);
            modelMacroQuality.Accuracy = Math.Round(accuracy,3);
            modelMacroQuality.MacroRecall = Math.Round(macrorecall,3);
            modelMacroQuality.MacroPrecision = Math.Round(marcoPrecision,3);
            ModelMacroQualityTable.Add(modelMacroQuality);
            //计算并生成混淆矩阵
            UpdateMatrix(valueKeys, o);
        }

        public void UpdateMatrix(List<(string actual, string predicted)> actual_predicteds,Grid _grid)
        {
            _grid.Children.Clear();
            _grid.RowDefinitions.Clear();
            _grid.ColumnDefinitions.Clear();
            _grid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;

            // 获取所有唯一的类别
            var categories = actual_predicteds
                .Select(x => x.actual)
                .Union(actual_predicteds.Select(x => x.predicted))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            int size = categories.Count;

            // 创建行和列定义（比实际多一行和一列用于标题）
            for (int i = 0; i <= size; i++)
            {
                _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            }

            // 添加标题行和列
            AddHeaderCell("实际\\预测", 0, 0, _grid);
            for (int i = 0; i < size; i++)
            {
                AddHeaderCell(categories[i], 0, i + 1, _grid); // 列标题
                AddHeaderCell(categories[i], i + 1, 0, _grid); // 行标题
            }

            // 计算混淆矩阵
            var matrix = new int[size, size];
            foreach (var (actual, predicted) in actual_predicteds)
            {
                int actualIndex = categories.IndexOf(actual);
                int predictedIndex = categories.IndexOf(predicted);
                matrix[actualIndex, predictedIndex]++;
            }

            // 填充混淆矩阵单元格
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    int value = matrix[i, j];
                    AddMatrixCell(value, i + 1, j + 1, i == j, _grid);
                }
            }
        }

        private void AddHeaderCell(string text, int row, int col, Grid _grid)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Margin = new Thickness(5),
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, col);
            _grid.Children.Add(textBlock);
        }

        private void AddMatrixCell(int value, int row, int col, bool isDiagonal, Grid _grid)
        {
            var border = new Border
            {
                Background = isDiagonal ? Brushes.LightGreen : Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(5)
            };

            var textBlock = new TextBlock
            {
                Text = value.ToString(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            _grid.Children.Add(border);
        }
        #endregion
    }
}
