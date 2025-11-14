using AutoTrainer.Helpers;
using AutoTrainer.Models;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
        public string? ClassName { get; set; }
        public string? ImagePath { get; set; }
        public IImage? Thumbnail { get; set; }
    }
    public partial class VisualVerifyViewModel : ViewModelBase
    {
        public VisualVerifyViewModel()
        {
            ValidDataImages = [];
            ClassifiedImages = [new PreviewImageModel() { ClassName = "类别"}];
            ModelSingleQualityTable = [new ModelSingleQuality() { ClassName = "0", Accuracy = 0, F1_score = 0, Precision = 0, Recall=0}];
            ModelMacroQualityTable = [new ModelMacroQuality() { Accuracy = 0, MacroPrecision = 0, MacroRecall = 0, MacroF1 = 0 }];
        }

        #region 可绑定属性
        [ObservableProperty]
        private bool isSelectCreateMutationValidData = true;
        [ObservableProperty]
        private string? validDatasFolderPath;
        [ObservableProperty]
        private ObservableCollection<MutationImage> validDataImages;
        [ObservableProperty]
        private ObservableCollection<PreviewImageModel> classifiedImages;
        [ObservableProperty]
        private bool isLoadingValidData = false;
        [ObservableProperty]
        private bool isInSortingTask = false;
        [ObservableProperty]
        private Thumbnail? selectImage;
        [ObservableProperty]
        private string? predictClass;
        [ObservableProperty]
        private string? confidence;
        [ObservableProperty]
        private string? actualClass;
        [ObservableProperty]
        private string? imagePath;
        [ObservableProperty]
        private bool isShowNextPageBtn = false;
        [ObservableProperty]
        private ObservableCollection<ModelSingleQuality> modelSingleQualityTable;
        [ObservableProperty]
        private ObservableCollection<ModelMacroQuality> modelMacroQualityTable;
        [ObservableProperty]
        private bool isSpinning = false;
        [ObservableProperty]
        private int validationImageRate = 15;
        #endregion

        #region 命令
        /// <summary>
        /// 选择包含验证数据的文件夹
        /// </summary>
        /// <remarks>如果用户取消文件夹选择或选择了无效文件夹，则应用程序状态不会发生任何变化。该方法会根据所选文件夹更新与验证数据路径和类计数相关的属性。</remarks>
        /// <param name="userControl">用于确定显示文件夹选择器对话框的顶级窗口上下文的用户界面控件。不能为空。</param>
        /// <returns>表示异步操作的任务。当验证数据已加载且应用程序状态已更新时，该任务完成。</returns>
        [RelayCommand]
        public async Task SelectedExistVarifyDatas(UserControl userControl)
        {
            var topLevel = TopLevel.GetTopLevel(userControl);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                AllowMultiple = false,
                Title = "选择验证集目录"
            });
            if(folders != null && folders.Count > 0)
            {
                var selectedFolder = folders[0];
                if(selectedFolder != null)
                {
                    var path = selectedFolder.Path.LocalPath;
                    if(Directory.Exists(path))
                    {
                        App.TrainModel.ClassifyValidImagesPath = path;
                        App.TrainModel.NumClasses = Directory.GetDirectories(path).Length;
                        ValidDatasFolderPath = path;
                        IsSpinning = true;
                        IsLoadingValidData = true;
                        await Task.Run(LoadMutationData);
                        IsLoadingValidData = false;
                        IsSpinning = false;
                    }
                }
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
            var venvFolder = App.PythonVenvPath;
            sb.Append($"--image-folder {App.MutationDataPath}");
            sb.Append($" --model-path {modelPath}");
            sb.Append($" --model-name {App.TrainModel.PretrainedModel}");
            sb.Append($" --image-folder {App.MutationDataPath}");
            sb.Append($" --num-classes {App.TrainModel.NumClasses}");
            sb.Append($" --output-path {specialPyLogPath}");
            var argument = sb.ToString();
            IsInSortingTask = true;
            await CliWrapHelper.ExecutePythonScriptAsync(classifyPyFilePath, venvFolder, argument, false);
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
                            if (string.IsNullOrEmpty(result.imagePath)) continue;
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
                    CalculateModelPerformance(classifyResult, o);
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
        private async Task RefreshValidDataImages()
        {
            if(!IsInSortingTask && !IsLoadingValidData && !IsSpinning)
            {
                IsSpinning = true;
                IsLoadingValidData = true;
                if (IsSelectCreateMutationValidData)
                {
                    await Task.Run(LoadMutationData);
                }
                else
                {
                    await Task.Run(LoadSelectedValidDatas);
                }
                IsLoadingValidData = false;
                IsSpinning = false;
            }
        }
        #endregion

        #region 函数
        /// <summary>
        /// 加载选择的验证图像
        /// </summary>
        public void LoadSelectedValidDatas()
        {
            var dataSetPath = ValidDatasFolderPath;
            if (IsSelectCreateMutationValidData)
            {
                return;
            }
            if (string.IsNullOrEmpty(dataSetPath) || !Directory.Exists(dataSetPath))
            {
                return;
            }

            ValidDataImages = [];
            var typeClasses = Directory.GetDirectories(dataSetPath);
            if (typeClasses.Length > 0)
            {
                App.TrainModel.NumClasses = typeClasses.Length;
                foreach (var classDir in typeClasses)
                {
                    var className = new DirectoryInfo(classDir).Name;
                    var files = Directory.GetFiles(classDir)
                        .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".bmp")).ToList();

                    foreach (var file in files)
                    {
                        using (var stream = System.IO.File.OpenRead(file))
                        {
                            var bitmap = new Bitmap(stream);
                            var thumbnail = ResizeBitmap(bitmap, 64, 64); // 调整为缩略图尺寸
                            ValidDataImages.Add(new MutationImage
                            {
                                ClassName = className,
                                ImagePath = file,
                                Thumbnail = thumbnail
                            });
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 加载变异图像
        /// </summary>
        public void LoadMutationData()
        {
            var dataSetPath = App.TrainModel.ClassifyTrainImagesPath;
            if (!IsSelectCreateMutationValidData)
            {
                return;
            }
            if (string.IsNullOrEmpty(dataSetPath))
            {
                return;
            }
            ValidDataImages = [];
            var typeClasses = Directory.GetDirectories(dataSetPath);
            if (typeClasses.Length > 0)
            {
                App.TrainModel.NumClasses = typeClasses.Length;
                App.TrainModel.ClassifyTrainImagesPath = dataSetPath;
                var files = new List<string>();
                Dictionary<string,int> classNameToIndexDic = [];
                for (int i = 0;i< typeClasses.Length;i++)
                {
                    classNameToIndexDic.Add(typeClasses[i].Split(App.Separator).Last(),i);
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
                    var count = tofiles[i].Split(App.Separator);
                    var className = count[count.Length - 2];
                    var index = classNameToIndexDic[className];
                    bool[] Augmentations = [true, true, false, false, true, true];
                    ImageAugmentation.AugmentImageOne(index, Augmentations, tofiles[i], App.MutationDataPath, 1);
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
                        ValidDataImages.Add(new MutationImage
                        {
                            ClassName = match.Groups[1].Value,
                            ImagePath = mutationData,
                            Thumbnail = thumbnail
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
        /// <summary>
        /// 随机重新排序指定列表中的元素
        /// </summary>
        /// <remarks>此方法会就地修改输入列表，并且不会返回新列表。
        /// 随机排序是使用随机数生成器执行的，因此每次调用时元素的顺序都会有所不同。
        /// 此方法不是线程安全的。</remarks>
        /// <typeparam name="T">列表中要随机排列的元素的类型。</typeparam>
        /// <param name="list">元素将被随机重新排序的列表。不能为空。</param>
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
                var actual = ValidDataImages.First(t => t.ImagePath == result.imagePath).ClassName;
                if (actual != null && predicted != null)
                {
                    valueKeys.Add((actual, predicted));
                }
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
        /// <summary>
        /// 更新混淆矩阵
        /// </summary>
        /// <param name="actual_predicteds"></param>
        /// <param name="_grid"></param>
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
        /// <summary>
        /// 将具有指定文本的粗体、居中标题单元格添加到指定网格的指定行和列
        /// </summary>
        /// <param name="text">要在标题单元格中显示的文本。</param>
        /// <param name="row">放置标题单元格的从零开始的行索引。</param>
        /// <param name="col">放置标题单元格的从零开始的列索引。</param>
        /// <param name="_grid">将添加标题单元格的网格。不能为空。</param>
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
        /// <summary>
        /// 添加一个矩阵单元格到指定网格的指定行和列。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="isDiagonal"></param>
        /// <param name="_grid"></param>
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
