using AutoTrainer.ControlHelper;
using AutoTrainer.Extension;
using AutoTrainer.Helpers;
using AutoTrainer.Models;
using AutoTrainer.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Notification;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Newtonsoft.Json;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class DatasetAnnotationViewModel : ViewModelBase
    {
        #region 全局属性
        public Canvas ImageCanvas;
        // 操作历史（撤销重做）
        private Stack<List<AnnotationItem>> undoStack = new();
        private Stack<List<AnnotationItem>> redoStack = new();
        // 导航状态
        public bool CanGoPrevious => CurrentImageIndex > 0;
        public bool CanGoNext => CurrentImageIndex < ImageList.Count - 1;
        // 是否有选中的标注
        public bool HasSelectedAnnotation => SelectedAnnotation != null;
        public bool HasNoSelectedAnnotation => !HasSelectedAnnotation;
        // 图片文件夹
        public string Imagefolder = string.Empty;
        // 标注文件名
        public string AnnotationFileName = "AnnotationConfig.json";
        // 存储所有图片的标注数据
        public Dictionary<string, List<AnnotationItem>> AllImageAnnotations = [];
        // 存储上次导入的COCO数据，用于模式切换时重新处理
        private CocoDataset? _lastImportedCocoDataset;
        #endregion

        #region 可绑定字段属性
        // 标注模式
        [ObservableProperty]
        private AnnotationMethodEnum currentMode = AnnotationMethodEnum.Manual;

        // 标注工具
        [ObservableProperty]
        private AnnotationToolEnum currentTool = AnnotationToolEnum.Rectangle;

        // 当前图像
        [ObservableProperty]
        private Bitmap? currentImage;

        [ObservableProperty]
        private string currentImagePath = string.Empty;

        [ObservableProperty]
        private string currentImageFileName = string.Empty;

        [ObservableProperty]
        private string currentImageSize = string.Empty;

        // 图像列表
        [ObservableProperty]
        private ObservableCollection<ImageItem> imageList = new();

        [ObservableProperty]
        private int currentImageIndex = -1;

        // 类别管理
        [ObservableProperty]
        private ObservableCollection<string> classNames = new();

        [ObservableProperty]
        private string selectedClassName = string.Empty;

        [ObservableProperty]
        private string newClassName = string.Empty;

        // 标注数据
        [ObservableProperty]
        private ObservableCollection<AnnotationItem> currentImageAnnotations = new();

        [ObservableProperty]
        private AnnotationItem? selectedAnnotation;

        [ObservableProperty]
        private string currentClassName = "default";

        [ObservableProperty]
        private bool isDrawing = false;

        [ObservableProperty]
        private AnnotationItem? currentDrawingItem = null;

        // 多边形绘制状态
        [ObservableProperty]
        private bool isDrawingPolygon = false;

        // 存储绘制的起始点
        public Avalonia.Point? drawingStartPoint = null;

        [ObservableProperty]
        private bool isCreatingTemplate = false;

        // UI状态
        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string progressState = "就绪";

        [ObservableProperty]
        private int progressValue = 0;

        [ObservableProperty]
        private int progressMax = 100;

        // 统计信息
        [ObservableProperty]
        private int totalImageCount = 0;

        [ObservableProperty]
        private int annotatedImageCount = 0;

        [ObservableProperty]
        private int totalAnnotationCount = 0;

        [ObservableProperty]
        private bool canUndo = false;

        [ObservableProperty]
        private bool canRedo = false;

        [ObservableProperty]
        private bool isApplyAsTemplate = false;

        [ObservableProperty]
        private bool isCroppingInProgress = false;

        [ObservableProperty]
        private bool isShowCroppingText = true;

        [ObservableProperty]
        private int finishedCroppingCount = 0;

        [ObservableProperty]
        private NotificationMessageManager notifyManager;

        [ObservableProperty]
        private ObservableCollection<string> _currentImageClasses = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBatchProcessing))]
        private bool isBatchProcessing = false;
        public bool IsNotBatchProcessing => !IsBatchProcessing;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddImageClassCommand))]
        private string? _selectedClassForAction;

        public bool CanAddImageClass => !string.IsNullOrEmpty(SelectedClassForAction) && !HasSelectedAnnotation;

        /// <summary>
        /// 是否勾选检测框标注类型
        /// </summary>
        [ObservableProperty]
        private bool isCheckDetectionRectType = true;

        [ObservableProperty]
        private string trainDatasetPath = string.Empty;
        #endregion

        #region 事件和委托
        public Action<AnnotationItem>? OnAnnotationSelected;
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="canvas"></param>
        public DatasetAnnotationViewModel(Canvas canvas)
        {
            ImageCanvas = canvas;
            // 初始化默认类别
            ClassNames.Add("默认类别");
            SelectedClassName = ClassNames.FirstOrDefault() ?? string.Empty;
            OnAnnotationSelected += SelectedAnnvationChanged;
            // 监听属性变化
            PropertyChanged += DatasetAnnotationViewModel_PropertyChanged;
            //初始化通知管理器
            NotifyManager = new NotificationMessageManager();
            CurrentImageAnnotations.CollectionChanged += CurrentImageAnnotations_CollectionChanged;
        }

        private void DatasetAnnotationViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentImageIndex):
                    OnPropertyChanged(nameof(CanGoPrevious));
                    OnPropertyChanged(nameof(CanGoNext));
                    LoadCurrentImage();
                    // Proactively load thumbnails around the selected item
                    _ = LoadThumbnailsInRange(Math.Max(0, CurrentImageIndex - 15), 30);
                    break;
                case nameof(SelectedAnnotation):
                    OnPropertyChanged(nameof(HasSelectedAnnotation));
                    OnPropertyChanged(nameof(HasNoSelectedAnnotation));
                    AddImageClassCommand.NotifyCanExecuteChanged();
                    if (SelectedAnnotation != null)
                    {
                        SelectedClassForAction = SelectedAnnotation.ClassName;
                    }
                    break;
            }
        }

        partial void OnSelectedClassForActionChanged(string? value)
        {
            if (SelectedAnnotation != null && SelectedAnnotation.ClassName != value)
            {
                SelectedAnnotation.ClassName = value;
            }
            AddImageClassCommand.NotifyCanExecuteChanged();
        }



        [RelayCommand]
        private void SelectManualMode()
        {
            CurrentMode = AnnotationMethodEnum.Manual;
            IsCreatingTemplate = false;
        }

        [RelayCommand]
        private void SelectAIMode()
        {
            CurrentMode = AnnotationMethodEnum.AIAssisted;
            // TODO: 实现AI辅助标注
        }

        [RelayCommand]
        private void SelectRectangleTool()
        {
            CurrentTool = AnnotationToolEnum.Rectangle;
        }

        [RelayCommand]
        private void SelectPolygonTool()
        {
            CurrentTool = AnnotationToolEnum.Polygon;
        }

        [RelayCommand]
        private void SelectPointTool()
        {
            CurrentTool = AnnotationToolEnum.Point;
        }

        /// <summary>
        /// 选择识别框标签模式
        /// </summary>
        [RelayCommand]
        private void SelectDetectionRectType()
        {
            IsCheckDetectionRectType = true;
            NotifyManager.CreateMessage()
                .Accent("#161616")
                .Background("#e5e4e2")
                .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                .HasBadge("Info")
                .HasMessage("已切换到识别框标签模式")
                .Dismiss().WithDelay(2000, t => { })
                .Queue();

            // 如果有导入的COCO数据，重新处理标注
            ReprocessAnnotationsFromCOCOData();
        }

        /// <summary>
        /// 选择实例分割标签模式
        /// </summary>
        [RelayCommand]
        private void SelectInstanceSegmentType()
        {
            IsCheckDetectionRectType = false;
            NotifyManager.CreateMessage()
                .Accent("#161616")
                .Background("#e5e4e2")
                .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                .HasBadge("Info")
                .HasMessage("已切换到实例分割标签模式")
                .Dismiss().WithDelay(2000, t => { })
                .Queue();

            // 如果有导入的COCO数据，重新处理标注
            ReprocessAnnotationsFromCOCOData();
        }

        /// <summary>
        /// 导入图像目录
        /// </summary>
        /// <param name="control"></param>
        [RelayCommand]
        private async Task ImportImageFolder(UserControl control)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    AllowMultiple = false,
                    Title = "选择图像目录"
                });

                if (folders?.Count > 0)
                {
                    var selectedFolder = folders[0];
                    Imagefolder = folders[0].TryGetLocalPath() ?? string.Empty;
                    await LoadImagesFromFolder(selectedFolder.Path);
                    TrainDatasetPath = Imagefolder;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"导入目录图像失败: {ex.Message}");
                // Consider adding user notification
            }
        }

        /// <summary>
        /// 导入COCO标注文件
        /// </summary>
        /// <param name="control">用于显示文件选择对话框的控件</param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ImportCOCOFile(UserControl control)
        {
            try
            {
                // 检查是否已导入图片
                if (!ImageList.Any())
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Warning")
                        .HasMessage("请先导入图片目录，然后再导入COCO标注文件。")
                        .Dismiss().WithDelay(4000, t => { })
                        .Queue();
                    return;
                }

                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel == null) return;

                // 显示文件选择对话框
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "选择COCO标注文件",
                    FileTypeFilter = [new("JSON文件") { Patterns = ["*.json"] }]
                });

                if (files?.Count > 0)
                {
                    var selectedFile = files[0].TryGetLocalPath();
                    if (string.IsNullOrEmpty(selectedFile)) return;

                    IsLoading = true;
                    ProgressState = "正在导入COCO标注文件...";
                    ProgressValue = 0;
                    ProgressMax = ImageList.Count;

                    await Task.Run(() => ImportCOCOData(selectedFile));

                    NotifyManager.CreateMessage()
                        .Accent("#161616")
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Success")
                        .HasMessage($"COCO标注文件导入成功：{System.IO.Path.GetFileName(selectedFile)}")
                        .Dismiss().WithDelay(5000, t => { })
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"导入COCO标注失败: {ex.Message}");
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Error")
                    .HasMessage($"导入失败：{ex.Message}")
                    .Dismiss().WithDelay(6000, t => { })
                    .Queue();
            }
            finally
            {
                IsLoading = false;
                ProgressState = "就绪";
            }
        }

        /// <summary>
        /// 切换到上一张图像
        /// </summary>
        [RelayCommand]
        private void PreviousImage()
        {
            if (CanGoPrevious)
            {
                CurrentImageIndex--;
            }
        }

        /// <summary>
        /// 切换到下一张图像
        /// </summary>
        [RelayCommand]
        private void NextImage()
        {
            if (CanGoNext)
            {
                CurrentImageIndex++;
            }
        }

        /// <summary>
        /// 添加类别
        /// </summary>
        [RelayCommand]
        private void AddClass()
        {
            if (!string.IsNullOrWhiteSpace(NewClassName) && !ClassNames.Contains(NewClassName))
            {
                ClassNames.Add(NewClassName);
                NewClassName = string.Empty;
            }
        }

        /// <summary>
        /// 移除类别
        /// </summary>
        /// <param name="className"></param>
        [RelayCommand]
        private void RemoveClass(string className)
        {
            if (ClassNames.Contains(className) && ClassNames.Count > 1)
            {
                ClassNames.Remove(className);

                // 如果删除的是当前选中的类别，切换到第一个
                if (SelectedClassName == className)
                {
                    SelectedClassName = ClassNames.FirstOrDefault() ?? string.Empty;
                }

                // 更新现有标注中使用该类别的项
                foreach (var annotation in CurrentImageAnnotations.Where(a => a.ClassName == className))
                {
                    annotation.ClassName = SelectedClassName;
                }
            }
        }

        /// <summary>
        /// 添加图像分类
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddImageClass))]
        private void AddImageClass()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ImageList.Count)
                return;

            var currentImageItem = ImageList[CurrentImageIndex];
            if (SelectedClassForAction != null && !currentImageItem.ImageClasses.Contains(SelectedClassForAction))
            {
                currentImageItem.ImageClasses.Add(SelectedClassForAction);
                // Also update the UI-bound collection
                CurrentImageClasses.Add(SelectedClassForAction);
            }
        }

        /// <summary>
        /// 移除图像分类
        /// </summary>
        /// <param name="className"></param>
        [RelayCommand]
        private void RemoveImageClass(string className)
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ImageList.Count)
                return;

            var currentImageItem = ImageList[CurrentImageIndex];
            if (currentImageItem.ImageClasses.Contains(className))
            {
                currentImageItem.ImageClasses.Remove(className);
                // Also update the UI-bound collection
                CurrentImageClasses.Remove(className);
            }
        }

        /// <summary>
        /// 清除所有标注
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        private void ClearAllAnnotations(Canvas canvas)
        {
            if (CurrentImageAnnotations.Any())
            {
                SaveToUndoStack();
                CurrentImageAnnotations.Clear();
                canvas.Children.Clear();
                UpdateStatistics();
            }
        }

        /// <summary>
        /// 复制标注
        /// </summary>
        [RelayCommand]
        private void CopyAnnotations()
        {
            // TODO: 实现复制当前图像的标注到剪贴板或下一张图像
        }

        /// <summary>
        /// 删除选中的标注
        /// </summary>
        /// <param name="canvas"></param>
        [RelayCommand]
        private void DeleteSelectedAnnotation()
        {
            if (SelectedAnnotation != null)
            {
                SaveToUndoStack();
                if (SelectedAnnotation.GetType() == typeof(RectangleModel))
                {
                    // 删除矩形标注
                    var rectangle = (RectangleModel)SelectedAnnotation;
                    if (rectangle != null)
                    {
                        var rectItem = ImageCanvas.Children.FirstOrDefault(t => t is Avalonia.Controls.Shapes.Rectangle rect && rect.Tag?.ToString() == rectangle.InstanceGuid);
                        if (rectItem != null)
                        {
                            ImageCanvas.Children.Remove(rectItem);
                        }
                    }
                }
                else if (SelectedAnnotation.GetType() == typeof(PolygonModel))
                {
                    // 删除多边形标注
                    var polygon = (PolygonModel)SelectedAnnotation;
                    if (polygon != null)
                    {
                        var polyItem = ImageCanvas.Children.FirstOrDefault(t => t is Polygon poly && poly.Tag?.ToString() == polygon.InstanceGuid);
                        if (polyItem != null)
                        {
                            ImageCanvas.Children.Remove(polyItem);
                        }
                    }
                }
                else if (SelectedAnnotation.GetType() == typeof(PointModel))
                {
                    var pointModel = (PointModel)SelectedAnnotation;
                    // 删除点标注
                    var pointItem = ImageCanvas.Children.FirstOrDefault(t => t is Ellipse ellipse && ellipse.Tag?.ToString() == pointModel.InstanceGuid);
                    if (pointItem != null)
                    {
                        ImageCanvas.Children.Remove(pointItem);
                    }
                }

                if (AllImageAnnotations.TryGetValue(CurrentImageFileName, out var annotationItems))
                {
                    annotationItems.Remove(SelectedAnnotation);
                }
                CurrentImageAnnotations.Remove(SelectedAnnotation);
                SelectedAnnotation = null;
                UpdateStatistics();
            }
        }

        /// <summary>
        /// 撤销
        /// </summary>
        [RelayCommand]
        private void Undo()
        {
            if (CanUndo && undoStack.Count > 0)
            {
                // 保存当前状态到重做栈
                redoStack.Push(new List<AnnotationItem>(CurrentImageAnnotations));

                // 恢复上一个状态
                var previousState = undoStack.Pop();
                CurrentImageAnnotations.Clear();
                foreach (var item in previousState)
                {
                    CurrentImageAnnotations.Add(item);
                }

                UpdateStackStates();
                UpdateStatistics();
            }
        }

        /// <summary>
        /// 重做
        /// </summary>
        [RelayCommand]
        private void Redo()
        {
            if (CanRedo && redoStack.Count > 0)
            {
                // 保存当前状态到撤销栈
                undoStack.Push(new List<AnnotationItem>(CurrentImageAnnotations));

                // 恢复重做状态
                var nextState = redoStack.Pop();
                CurrentImageAnnotations.Clear();
                foreach (var item in nextState)
                {
                    CurrentImageAnnotations.Add(item);
                }

                UpdateStackStates();
                UpdateStatistics();
            }
        }

        /// <summary>
        /// 保存当前图片的标注或将其作为分类数据
        /// </summary>
        [RelayCommand]
        private void SaveCurrentAnnotation()
        {
            if (CurrentImageAnnotations.Any())
            {
                // If there are annotations, they are already saved in memory.
                NotifyManager.CreateMessage()
                    .Accent("#161616")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Info")
                    .HasMessage("当前图片的标注已在内存中，切换图片时会自动保存。")
                    .Dismiss().WithDelay(3000, t => { })
                    .Queue();
            }
            else
            {
                // If there are no annotations, save the image to the folders of its assigned classes.
                if (CurrentImageClasses.Count == 0)
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Warning")
                        .HasMessage("请先使用右侧的 '+' 按钮为此图片添加分类。")
                        .Dismiss().WithDelay(4000, t => { })
                        .Queue();
                    return;
                }

                try
                {
                    var classifiedImagesPath = System.IO.Path.Combine(Environment.CurrentDirectory, "DataSet", "ClassifiedImages");
                    foreach (var className in CurrentImageClasses)
                    {
                        var classPath = System.IO.Path.Combine(classifiedImagesPath, className);
                        Directory.CreateDirectory(classPath);
                        var destFileName = System.IO.Path.Combine(classPath, CurrentImageFileName);
                        File.Copy(CurrentImagePath, destFileName, true); // true to overwrite
                    }

                    NotifyManager.CreateMessage()
                        .Accent("#161616")
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Success")
                        .HasMessage($"图片已分类到: {string.Join(", ", CurrentImageClasses)}")
                        .Dismiss().WithButton("打开总目录", button =>
                        {
                            FileDirectoryHelper.OpenInExplorer(classifiedImagesPath, false);
                        })
                        .Dismiss().WithDelay(6000, t => { })
                        .Queue();
                }
                catch (Exception ex)
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Error")
                        .HasMessage($"分类保存失败: {ex.Message}")
                        .Dismiss().WithDelay(6000, t => { })
                        .Queue();
                }
            }
        }

        /// <summary>
        /// 保存标注数据到本地文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void SaveAllAnnotations()
        {
            try
            {
                //在图片目录创建AnnotationConfig.json文件 保存每一张图片的标注数据
                if (CurrentImageIndex < 0 || CurrentImageIndex >= ImageList.Count)
                {
                    return;
                }
                var annotationPath = System.IO.Path.Combine(Imagefolder, AnnotationFileName);
                AllImageAnnotations.SaveToFile(annotationPath);
                NotifyManager.CreateMessage()
                    .Accent("#161616")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Info")
                    .HasMessage($"保存标注配置完成：{annotationPath}")
                    .Dismiss().WithButton("打开文件夹", button =>
                    {
                        FileDirectoryHelper.OpenInExplorer(annotationPath, true);
                    })
                    .Dismiss().WithDelay(6000, t => { })
                    .Queue();
            }
            catch (Exception ex)
            {
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Error")
                    .HasMessage($"保存失败：{ex.Message}")
                    .Dismiss().WithButton("复制信息", button =>
                    {
                        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            var clipboard = desktop.MainWindow?.Clipboard;
                            clipboard?.SetTextAsync(ex.Message);  // 复制纯文本
                        }
                    })
                    .Dismiss().WithDelay(6000, t => { })
                    .Queue();
                ProgressState = $"保存失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 创建模板
        /// </summary>
        [RelayCommand]
        private void CreateTemplate()
        {
            IsCreatingTemplate = true;
            CurrentMode = AnnotationMethodEnum.Template;
            // TODO: 进入模板创建模式
        }

        /// <summary>
        /// 保存模板
        /// </summary>
        [RelayCommand]
        private void SaveTemplate()
        {

        }

        /// <summary>
        /// 应用模板
        /// </summary>
        [RelayCommand]
        private void ApplyTemplate()
        {

        }

        /// <summary>
        /// 批次应用模板
        /// </summary>
        [RelayCommand]
        private void BatchApplyTemplate()
        {

        }

        /// <summary>
        /// 导入分类标签文件
        /// </summary>
        /// <param name="control"></param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ImportClassesAnnotations(UserControl control)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel == null)
                {
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "选择分类标签文件",
                    FileTypeFilter = [new("Text files") { Patterns = ["*.txt"] }]
                });

                if (files?.Count > 0)
                {
                    var selectedFile = files[0].TryGetLocalPath();
                    if (string.IsNullOrEmpty(selectedFile)) return;

                    var (successCount, errorCount, newClasses) = AnnotationFileHelper.ImportClassifications(selectedFile, ImageList, ClassNames);

                    if (successCount == -1)
                    {
                        NotifyManager.CreateMessage()
                            .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                            .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                            .Background("#e5e4e2")
                            .HasBadge("Error")
                            .HasMessage("读取文件失败。")
                            .Dismiss().WithDelay(6000, t => { })
                            .Queue();
                        return;
                    }

                    //向主集合添加新类
                    foreach (var newClass in newClasses)
                    {
                        if (!ClassNames.Contains(newClass))
                        {
                            ClassNames.Add(newClass);
                        }
                    }

                    //刷新当前受到影响的图像，给它们加上匹配的类别
                    if (CurrentImageIndex != -1)
                    {
                        var currentImageItem = ImageList[CurrentImageIndex];
                        CurrentImageClasses.Clear();
                        foreach (var cls in currentImageItem.ImageClasses)
                        {
                            CurrentImageClasses.Add(cls);
                        }
                    }

                    NotifyManager.CreateMessage()
                        .Accent("#161616")
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Info")
                        .HasMessage($"导入结果: {successCount}条成功, {errorCount}条失败。新增类别: {newClasses.Count}个。")
                        .Dismiss().WithDelay(6000)
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Error")
                    .HasMessage($"导入失败: {ex.Message}")
                    .Dismiss().WithDelay(6000)
                    .Queue();
            }
        }

        /// <summary>
        /// 导出COCO标注文件
        /// </summary>
        /// <param name="control">用于显示文件保存对话框的控件</param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ExportCOCOFile(UserControl control)
        {
            try
            {
                // 检查是否有图片和标注数据
                if (!ImageList.Any())
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Warning")
                        .HasMessage("没有图片数据可导出。")
                        .Dismiss().WithDelay(4000, t => { })
                        .Queue();
                    return;
                }

                // 检查是否有标注数据
                var hasAnnotations = AllImageAnnotations.Any() || ImageList.Any(img => img.IsAnnotated);
                if (!hasAnnotations)
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Warning")
                        .HasMessage("没有标注数据可导出。")
                        .Dismiss().WithDelay(4000, t => { })
                        .Queue();
                    return;
                }

                // 检查是否只支持矩形框和多边形标注
                var hasUnsupportedAnnotations = AllImageAnnotations.Values.Any(annotations =>
                    annotations.Any(ann => ann.AnnotationType == AnnotationToolEnum.Point));

                if (hasUnsupportedAnnotations)
                {
                    NotifyManager.CreateMessage()
                        .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasBadge("Warning")
                        .HasMessage("当前只支持矩形框和多边形标注的COCO导出，点标注将被忽略。")
                        .Dismiss().WithDelay(5000, t => { })
                        .Queue();
                }

                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel == null) return;

                // 显示文件保存对话框
                var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "保存COCO标注文件",
                    SuggestedFileName = "annotations.json",
                    FileTypeChoices = [new("JSON文件") { Patterns = ["*.json"] }]
                });

                if (saveFile == null) return;

                var savePath = saveFile.TryGetLocalPath();
                if (string.IsNullOrEmpty(savePath)) return;

                // 开始导出
                ProgressState = "正在导出COCO标注文件...";
                IsLoading = true;

                await Task.Run(() => ExportToCOCOFormat(savePath));

                NotifyManager.CreateMessage()
                    .Accent("#161616")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Success")
                    .HasMessage($"COCO标注文件导出成功：{savePath}")
                    .Dismiss().WithButton("打开文件夹", button =>
                    {
                        var folder = System.IO.Path.GetDirectoryName(savePath);
                        if (!string.IsNullOrEmpty(folder))
                            FileDirectoryHelper.OpenInExplorer(folder, false);
                    })
                    .Dismiss().WithDelay(6000, t => { })
                    .Queue();
            }
            catch (Exception ex)
            {
                Log.Error($"导出COCO标注失败: {ex.Message}");
                Debug.WriteLine($"导出COCO标注失败: {ex.Message}");

                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Error")
                    .HasMessage($"导出失败：{ex.Message}")
                    .Dismiss().WithDelay(6000, t => { })
                    .Queue();
            }
            finally
            {
                IsLoading = false;
                ProgressState = "就绪";
            }
        }

        /// <summary>
        /// 裁剪数据集并分类保存
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task CroppingImgAsDataSet()
        {
            string baseOutputPath = System.IO.Path.Combine(Environment.CurrentDirectory, "CroppedImages");
            Directory.CreateDirectory(baseOutputPath);
            IsCroppingInProgress = true;
            IsShowCroppingText = false;
            await Task.Run(async () =>
            {
                try
                {
                    FinishedCroppingCount = 0;
                    foreach (var imageItem in ImageList)
                    {
                        if (string.IsNullOrEmpty(imageItem.FilePath) || !File.Exists(imageItem.FilePath))
                            continue;
                        // 获取当前图片的标注数据
                        var fileName = System.IO.Path.GetFileName(imageItem.FilePath);
                        // 根据是否应用为模板选择标注数据源
                        List<AnnotationItem> annotations = IsApplyAsTemplate
                            ? [.. CurrentImageAnnotations]
                            : AllImageAnnotations.TryGetValue(fileName, out var imageAnnotations)
                                ? imageAnnotations
                                : [];

                        if (annotations.Count == 0)
                        {
                            // Todo：提示用户没有标注
                            Console.WriteLine($"No annotations found for image {fileName}");
                            continue;
                        }

                        try
                        {
                            using var originalImage = SixLabors.ImageSharp.Image.Load(imageItem.FilePath);

                            foreach (var annotation in annotations)
                            {
                                // 忽略点标注
                                if (annotation is PointModel)
                                    continue;

                                // 获取边界框
                                var boundingBox = annotation.GetBoundingBox();
                                // 跳过无效边界框
                                if (boundingBox.Width <= 0 || boundingBox.Height <= 0)
                                    continue;

                                var className = annotation.ClassName ?? "Unknown";
                                var outputPath = System.IO.Path.Combine(baseOutputPath, className);
                                Directory.CreateDirectory(outputPath);

                                var cropRectangle = new SixLabors.ImageSharp.Rectangle(
                                    (int)boundingBox.X,
                                    (int)boundingBox.Y,
                                    (int)boundingBox.Width,
                                    (int)boundingBox.Height
                                );

                                // 防止越界裁剪
                                cropRectangle = SixLabors.ImageSharp.Rectangle.Intersect(cropRectangle, originalImage.Bounds);

                                if (cropRectangle.Width <= 0 || cropRectangle.Height <= 0)
                                    continue;

                                using var croppedImage = originalImage.Clone(ctx => ctx.Crop(cropRectangle));

                                SixLabors.ImageSharp.Image finalImage = croppedImage; // 默认使用矩形裁剪结果

                                if (annotation is PolygonModel polygon)
                                {
                                    // 对于多边形，使用掩码裁剪实际多边形区域
                                    var minX = boundingBox.X;
                                    var minY = boundingBox.Y;
                                    var translatedPoints = polygon.Points
                                        .Select(p => new SixLabors.ImageSharp.PointF((float)(p.X - minX), (float)(p.Y - minY)))
                                        .ToArray();

                                    var polyShape = new SixLabors.ImageSharp.Drawing.Polygon(
                                        new SixLabors.ImageSharp.Drawing.LinearLineSegment(translatedPoints)
                                    );

                                    var maskedImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                                        cropRectangle.Width, cropRectangle.Height
                                    );

                                    var imageBrush = new SixLabors.ImageSharp.Drawing.Processing.ImageBrush(croppedImage);
                                    maskedImage.Mutate(ctx => ctx.Fill(imageBrush, polyShape));

                                    finalImage = maskedImage;
                                }

                                var croppedFileName = $"{System.IO.Path.GetFileNameWithoutExtension(fileName)}_{annotation.InstanceGuid}.png";
                                var savePath = System.IO.Path.Combine(outputPath, croppedFileName);

                                await finalImage.SaveAsPngAsync(savePath);

                                // 如果是多边形，释放 maskedImage
                                if (annotation is PolygonModel)
                                {
                                    finalImage.Dispose();
                                }
                            }
                            FinishedCroppingCount++;
                        }
                        catch (Exception ex)
                        {
                            // 可选：记录日志或提示错误
                            Debug.WriteLine($"Error processing image {imageItem.FilePath}: {ex.Message}");
                        }
                    }
                    App.TrainModel.TrainDataPath = baseOutputPath;
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        NotifyManager.CreateMessage()
                        .Accent("#161616")
                        .Background("#e5e4e2")
                        .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                        .HasMessage($"裁剪完成，数据集已创建并设置为训练路径。共裁剪 {FinishedCroppingCount} 张图片")
                        .HasBadge("Info")
                        .Dismiss().WithButton("查看分类总目录", button =>
                        {
                            FileDirectoryHelper.OpenInExplorer(baseOutputPath, false);
                        })
                        .Dismiss().WithDelay(6000)
                        .Queue();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error during cropping: {ex.Message}");
                }
                finally
                {
                    IsCroppingInProgress = false;
                    IsShowCroppingText = true;
                }
            });
        }

        /// <summary>
        /// 批量创建分类数据集
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task BatchCreateDataset()
        {
            if (!ImageList.Any())
            {
                NotifyManager.CreateMessage()
                    .HasBadge("Warning")
                    .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .Background("#e5e4e2")
                    .HasMessage("请先导入图片。")
                    .Dismiss().WithDelay(6000)
                    .Queue();
                return;
            }

            string baseOutputPath = System.IO.Path.Combine(Environment.CurrentDirectory, "DataSet", "ClassifiedImages");
            Directory.CreateDirectory(baseOutputPath);
            IsBatchProcessing = true;
            ProgressState = "开始批量生成分类数据集...";
            ProgressValue = 0;
            ProgressMax = ImageList.Count;

            try
            {
                await Task.Run(() =>
                {
                    int processedCount = 0;
                    foreach (var imageItem in ImageList)
                    {
                        if (string.IsNullOrEmpty(imageItem.FilePath) || !File.Exists(imageItem.FilePath) || !imageItem.ImageClasses.Any())
                        {
                            processedCount++;
                            Dispatcher.UIThread.InvokeAsync(() => ProgressValue = processedCount);
                            continue;
                        }

                        foreach (var className in imageItem.ImageClasses)
                        {
                            var classPath = System.IO.Path.Combine(baseOutputPath, className);
                            Directory.CreateDirectory(classPath);
                            var destFileName = System.IO.Path.Combine(classPath, imageItem.FileName);
                            try
                            {
                                File.Copy(imageItem.FilePath, destFileName, true); // true to overwrite
                            }
                            catch (Exception ex)
                            {
                                // Log error for this specific file
                                Debug.WriteLine($"Failed to copy {imageItem.FileName} to {className} folder: {ex.Message}");
                            }
                        }
                        processedCount++;
                        Dispatcher.UIThread.InvokeAsync(() => ProgressValue = processedCount);
                    }
                });

                App.TrainModel.TrainDataPath = baseOutputPath;
                App.TrainModel.NumClasses = Directory.GetDirectories(baseOutputPath).Length;
                NotifyManager.CreateMessage()
                    .Accent("#161616")
                    .HasBadge("Info")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasMessage($"批量生成成功！数据集已设置为训练路径。")
                    .Dismiss().WithButton("打开目录", button => FileDirectoryHelper.OpenInExplorer(baseOutputPath, false))
                    .Dismiss().WithDelay(6000)
                    .Queue();
            }
            catch (Exception ex)
            {
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .HasBadge("Error")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasMessage($"批量生成失败: {ex.Message}")
                    .Dismiss().WithDelay(6000)
                    .Queue();
            }
            finally
            {
                IsBatchProcessing = false;
                ProgressState = "就绪";
            }
        }


        #region 函数
        /// <summary>
        /// 更新UI(动态加载标注)
        /// </summary>
        /// <param name="annotationItem"></param>
        private void UpdateUI(AnnotationItem annotationItem)
        {
            switch (annotationItem)
            {
                case RectangleModel rectModel:
                    {
                        var rect = new Avalonia.Controls.Shapes.Rectangle
                        {
                            Tag = rectModel.InstanceGuid,
                            Stroke = Avalonia.Media.Brushes.Red,
                            StrokeThickness = 2,
                            Width = rectModel.Width,
                            Height = rectModel.Height
                        };
                        Canvas.SetLeft(rect, rectModel.X);
                        Canvas.SetTop(rect, rectModel.Y);
                        rectModel.UIElement = rect;
                        ImageCanvas.Children.Add(rect);
                    }
                    break;
                case PolygonModel polygonModel:
                    {
                        var poloygen = new Polygon
                        {
                            Tag = polygonModel.InstanceGuid,
                            StrokeThickness = 1,
                            Stroke = Avalonia.Media.Brushes.Blue,
                            Points = polygonModel.Points
                        };
                        polygonModel.UIElement = poloygen;
                        ImageCanvas.Children.Add(poloygen);
                    }
                    break;
                case PointModel pointModel:
                    {
                        var point = new Ellipse
                        {
                            Tag = pointModel.InstanceGuid,
                            Width = 4,
                            Height = 4,
                            Stroke = Avalonia.Media.Brushes.DarkGreen,
                            StrokeThickness = 2,
                        };
                        Canvas.SetLeft(point, pointModel.X - 4);
                        Canvas.SetTop(point, pointModel.Y - 4);
                        pointModel.UIElement = point;
                        ImageCanvas.Children.Add(point);
                    }
                    break;
            }
        }

        /// <summary>
        /// 加载当前图片
        /// </summary>
        private void LoadCurrentImage()
        {
            if (CurrentImageIndex >= 0 && CurrentImageIndex < ImageList.Count)
            {
                SelectedAnnotation = null; // 重置选中的标注
                SelectedClassForAction = null; // 重置类别选择
                var imageItem = ImageList[CurrentImageIndex];
                CurrentImagePath = imageItem.FilePath;
                CurrentImageFileName = System.IO.Path.GetFileName(imageItem.FilePath);
                try
                {
                    CurrentImage = new Bitmap(imageItem.FilePath);
                    CurrentImageSize = $"{CurrentImage.PixelSize.Width}x{CurrentImage.PixelSize.Height}";

                    // 加载该图像的标注数据
                    LoadAnnotationsForCurrentImage();

                    // 加载图像级别的分类数据
                    CurrentImageClasses.Clear();
                    foreach (var cls in imageItem.ImageClasses)
                    {
                        CurrentImageClasses.Add(cls);
                    }

                    OnPropertyChanged(nameof(SelectedClassForAction));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载图像失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 图像中选择的标注项发生变化调用
        /// </summary>
        /// <param name="annotation"></param>
        private void SelectedAnnvationChanged(AnnotationItem? annotation)
        {
            if (annotation != null)
            {
                annotation.UpdateBoundingBoxInfo();
                SelectedAnnotation = annotation;
            }
        }

        /// <summary>
        /// 从全局字典或本地文件加载当前图像的标注数据
        /// </summary>
        /// <returns></returns>
        private void LoadAnnotationsForCurrentImage()
        {
            CurrentImageAnnotations.Clear();
            ImageCanvas.Children.Clear();
            if (AllImageAnnotations.TryGetValue(CurrentImageFileName, out var annotations))
            {
                foreach (var annotation in annotations)
                {
                    CurrentImageAnnotations.Add(annotation);
                    UpdateUI(annotation);
                }
            }
            else
            {
                // 如果没有标注数据，再看看本地是否有保存的标注文件
                var annotationPath = System.IO.Path.Combine(Imagefolder, AnnotationFileName);
                if (File.Exists(annotationPath))
                {
                    try
                    {
                        //var jsonStr = await File.ReadAllTextAsync(annotationPath);
                        //var allAnnotations = JsonConvert.DeserializeObject<Dictionary<string, List<AnnotationItem>>>(jsonStr);
                        var allAnnotations = AnnotationFactory.LoadFromFile(annotationPath);
                        if (allAnnotations != null && allAnnotations.TryGetValue(CurrentImageFileName, out var fileAnnotations))
                        {
                            AllImageAnnotations = allAnnotations;
                            foreach (var annotation in fileAnnotations)
                            {
                                CurrentImageAnnotations.Add(annotation);
                                UpdateUI(annotation);
                            }
                            // 更新全局字典
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"加载标注文件失败: {ex.Message}");
                    }
                }

            }
            UpdateStatistics();
        }

        /// <summary>
        /// 保存当前标注状态到撤销栈
        /// </summary>
        private void SaveToUndoStack()
        {
            undoStack.Push(new List<AnnotationItem>(CurrentImageAnnotations));
            redoStack.Clear(); // 清空重做栈
            UpdateStackStates();
        }

        /// <summary>
        /// 更新撤销重做状态
        /// </summary>
        private void UpdateStackStates()
        {
            CanUndo = undoStack.Count > 0;
            CanRedo = redoStack.Count > 0;
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            TotalImageCount = ImageList.Count;
            AnnotatedImageCount = ImageList.Count(img => img.IsAnnotated);
            TotalAnnotationCount = ImageList.Sum(img => img.AnnotationCount);
        }

        private void CurrentImageAnnotations_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
            {
                // When a new annotation is added, select it immediately.
                var newItem = e.NewItems[e.NewItems.Count - 1] as AnnotationItem;
                if (newItem != null)
                {
                    SelectedAnnotation = newItem;
                }
            }
        }

        /// <summary>
        /// 以异步和虚拟方式加载图像。
        /// </summary>
        private async Task LoadImagesFromFolder(Uri folderUri)
        {
            IsLoading = true;
            ProgressState = "正在扫描文件路径...";
            ImageList.Clear();
            AllImageAnnotations.Clear(); // Also clear annotations from previous folder

            try
            {
                var imageItems = await Task.Run(() =>
                {
                    var directoryInfo = new DirectoryInfo(folderUri.LocalPath);
                    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".tif"
                    };

                    return directoryInfo.GetFiles()
                        .Where(f => supportedExtensions.Contains(f.Extension.ToLowerInvariant()))
                        .Select(f => f.FullName)
                        .OrderBy(f => f)
                        .Select(path => new ImageItem { FilePath = path, FileName = System.IO.Path.GetFileName(path) })
                        .ToList();
                });


                ProgressState = "正在加载图片列表...";
                ImageList = new ObservableCollection<ImageItem>(imageItems);

                if (ImageList.Any())
                {
                    // Set the first image as current, but don't load its main bitmap yet
                    CurrentImageIndex = 0;
                }

                // Asynchronously load thumbnails for the initial view
                _ = LoadThumbnailsInRange(0, 30); // Load first 30 thumbnails in background
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载图像失败: {ex.Message}");
                // Handle error
            }
            finally
            {
                IsLoading = false;
                ProgressState = "就绪";
            }
        }

        /// <summary>
        /// 异步加载 ImageList 中给定范围的项目的缩略图。
        /// </summary>
        private async Task LoadThumbnailsInRange(int startIndex, int count)
        {
            var itemsToLoad = ImageList.Skip(startIndex).Take(count).ToList();
            foreach (var item in itemsToLoad)
            {
                if (item.Thumbnail == null)
                {
                    try
                    {
                        item.Thumbnail = await CreateThumbnailAsync(item.FilePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"创建缩略图失败 {item.FileName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 从文件路径异步创建单个缩略图。
        /// </summary>
        private async Task<Bitmap?> CreateThumbnailAsync(string filePath, int maxSize = 150)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    using var originalBitmap = new Bitmap(stream);
                    var scale = Math.Min((double)maxSize / originalBitmap.PixelSize.Width,
                                       (double)maxSize / originalBitmap.PixelSize.Height);
                    var newWidth = (int)(originalBitmap.PixelSize.Width * scale);
                    var newHeight = (int)(originalBitmap.PixelSize.Height * scale);
                    return originalBitmap.CreateScaledBitmap(new PixelSize(newWidth, newHeight));
                }
                catch (Exception)
                {
                    // Return null or a placeholder "error" bitmap
                    return null;
                }
            });
        }

        /// <summary>
        /// “作为模板应用至全体图片”勾选状态改变
        /// </summary>
        /// <param name="value"></param>
        partial void OnIsApplyAsTemplateChanged(bool value)
        {
            ImageList[CurrentImageIndex].AsCroppingTemplate = value;
        }

        /// <summary>
        /// 当前选择图像索引改变
        /// </summary>
        /// <param name="value"></param>
        partial void OnCurrentImageIndexChanged(int value)
        {
            IsApplyAsTemplate = ImageList[value].AsCroppingTemplate;
        }

        /// <summary>
        /// 导出数据到COCO格式
        /// </summary>
        /// <param name="savePath">保存路径</param>
        private void ExportToCOCOFormat(string savePath)
        {
            var cocoDataset = new CocoDataset
            {
                Info = new Info
                {
                    Description = "AutoTrainer Dataset Annotations",
                    Url = "https://github.com/mehaifeng/AutoTrainer",
                    Version = "1.0",
                    Year = DateTime.Now.Year,
                    Contributor = "AutoTrainer",
                    DateCreated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                Licenses = new List<License>
                {
                    new License
                    {
                        Id = 1,
                        Name = "MIT",
                        Url = ""
                    }
                },
                Images = new List<COCOImage>(),
                Annotations = new List<Annotation>(),
                Categories = new List<Category>()
            };

            // 收集所有类别名称
            var allClassNames = new HashSet<string>();
            foreach (var annotations in AllImageAnnotations.Values)
            {
                foreach (var annotation in annotations)
                {
                    if (!string.IsNullOrEmpty(annotation.ClassName))
                    {
                        allClassNames.Add(annotation.ClassName);
                    }
                }
            }

            // 创建类别映射
            var categoryIdMap = new Dictionary<string, int>();
            int categoryId = 1;
            foreach (var className in allClassNames)
            {
                categoryIdMap[className] = categoryId;
                cocoDataset.Categories.Add(new Category
                {
                    Id = categoryId,
                    Name = className,
                    Supercategory = "object"
                });
                categoryId++;
            }

            // 创建图像和标注
            int imageId = 1;
            int annotationId = 1;

            foreach (var imageItem in ImageList)
            {
                if (string.IsNullOrEmpty(imageItem.FilePath) || !File.Exists(imageItem.FilePath))
                    continue;

                // 获取图像尺寸
                int imageWidth = 0, imageHeight = 0;
                try
                {
                    using var bitmap = new Bitmap(imageItem.FilePath);
                    imageWidth = bitmap.PixelSize.Width;
                    imageHeight = bitmap.PixelSize.Height;
                }
                catch
                {
                    // 如果无法读取图像尺寸，使用默认值
                    imageWidth = 1024;
                    imageHeight = 1024;
                }

                // 添加图像信息
                var cocoImage = new COCOImage
                {
                    Id = imageId,
                    Width = imageWidth,
                    Height = imageHeight,
                    FileName = imageItem.FileName
                };
                cocoDataset.Images.Add(cocoImage);

                // 获取该图像的标注
                var fileName = System.IO.Path.GetFileName(imageItem.FilePath);
                var annotations = AllImageAnnotations.TryGetValue(fileName, out var imageAnnotations)
                    ? imageAnnotations
                    : new List<AnnotationItem>();

                // 转换标注为COCO格式
                foreach (var annotation in annotations)
                {
                    // 只处理矩形框和多边形标注
                    if (annotation.AnnotationType == AnnotationToolEnum.Point)
                        continue;

                    if (string.IsNullOrEmpty(annotation.ClassName) || !categoryIdMap.ContainsKey(annotation.ClassName))
                        continue;

                    var cocoAnnotation = new Annotation
                    {
                        Id = annotationId,
                        ImageId = imageId,
                        CategoryId = categoryIdMap[annotation.ClassName],
                        IsCrowd = 0
                    };

                    // 根据标注类型正确处理bbox和segmentation
                    if (annotation.AnnotationType == AnnotationToolEnum.Rectangle && annotation is RectangleModel rectModel)
                    {
                        // 矩形框标注：使用实际矩形坐标和尺寸作为bbox
                        cocoAnnotation.Bbox =
                        [
                            Math.Round(rectModel.X, 2),
                            Math.Round(rectModel.Y, 2),
                            Math.Round(rectModel.Width, 2),
                            Math.Round(rectModel.Height, 2)
                        ];
                        cocoAnnotation.Area = Math.Round(rectModel.Width * rectModel.Height, 2);

                        // 将矩形框转换为多边形点作为segmentation
                        var rectSegmentation = new List<double>
                        {
                            Math.Round(rectModel.X, 2),
                            Math.Round(rectModel.Y, 2),
                            Math.Round(rectModel.X + rectModel.Width, 2),
                            Math.Round(rectModel.Y, 2),
                            Math.Round(rectModel.X + rectModel.Width, 2),
                            Math.Round(rectModel.Y + rectModel.Height, 2),
                            Math.Round(rectModel.X, 2),
                            Math.Round(rectModel.Y + rectModel.Height, 2)
                        };

                        cocoAnnotation.Segmentation = new Segmentation
                        {
                            Polygons = [rectSegmentation]
                        };
                    }
                    else if (annotation.AnnotationType == AnnotationToolEnum.Polygon && annotation is PolygonModel polygon)
                    {
                        // 多边形标注：bbox设为空数组，segmentation使用实际顶点
                        cocoAnnotation.Bbox = [];

                        // 多边形分割：使用实际顶点坐标
                        var segmentation = new List<double>();
                        foreach (var point in polygon.Points)
                        {
                            segmentation.Add(Math.Round(point.X, 2));
                            segmentation.Add(Math.Round(point.Y, 2));
                        }

                        if (segmentation.Count > 0)
                        {
                            cocoAnnotation.Segmentation = new Segmentation
                            {
                                Polygons = new List<List<double>> { segmentation }
                            };

                            // 计算多边形面积作为area
                            cocoAnnotation.Area = Math.Round(CalculatePolygonArea([.. polygon.Points]), 2);
                        }
                        else
                        {
                            // 如果多边形没有有效点，设为空数组
                            cocoAnnotation.Segmentation = new Segmentation
                            {
                                Polygons = new List<List<double>>()
                            };
                            cocoAnnotation.Area = 0;
                        }
                    }
                    else if (annotation.AnnotationType == AnnotationToolEnum.Point && annotation is PointModel)
                    {
                        //TODO 以后支持点标注
                        continue;
                    }

                    cocoDataset.Annotations.Add(cocoAnnotation);
                    annotationId++;
                }
                imageId++;
            }

            // 保存为JSON文件
            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter> { new SegmentationConverter() }
            };

            var json = JsonConvert.SerializeObject(cocoDataset, jsonSettings);
            File.WriteAllText(savePath, json);
        }

        /// <summary>
        /// 导入COCO数据
        /// </summary>
        /// <param name="filePath">COCO文件路径</param>
        private void ImportCOCOData(string filePath)
        {
            try
            {
                // 读取并解析COCO文件
                var jsonContent = File.ReadAllText(filePath);
                var settings = new JsonSerializerSettings
                {
                    Converters = { new SegmentationConverter() },
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var cocoDataset = JsonConvert.DeserializeObject<CocoDataset>(jsonContent, settings);

                if (cocoDataset?.Annotations == null || cocoDataset?.Images == null || cocoDataset?.Categories == null)
                {
                    throw new InvalidOperationException("无效的COCO文件格式");
                }

                // 存储COCO数据，用于模式切换时重新处理
                _lastImportedCocoDataset = cocoDataset;

                // 创建图像ID到文件名的映射
                var imageIdMap = new Dictionary<int, string>();
                var categoryIdMap = new Dictionary<int, string>();

                // 构建图像映射
                foreach (var image in cocoDataset.Images)
                {
                    imageIdMap[image.Id] = image.FileName;
                }

                // 构建类别映射
                foreach (var category in cocoDataset.Categories)
                {
                    categoryIdMap[category.Id] = category.Name;

                    // 添加新类别到类别列表
                    if (!ClassNames.Contains(category.Name))
                    {
                        Dispatcher.UIThread.Invoke(() => ClassNames.Add(category.Name));
                    }
                }

                // 按图像分组标注
                var annotationsByImage = new Dictionary<string, List<Annotation>>();
                foreach (var annotation in cocoDataset.Annotations)
                {
                    if (!imageIdMap.ContainsKey(annotation.ImageId))
                        continue;

                    var fileName = imageIdMap[annotation.ImageId];
                    if (!annotationsByImage.ContainsKey(fileName))
                    {
                        annotationsByImage[fileName] = new List<Annotation>();
                    }
                    annotationsByImage[fileName].Add(annotation);
                }

                int processedCount = 0;
                AllImageAnnotations.Clear();

                // 处理每个图像的标注
                foreach (var imageItem in ImageList)
                {
                    var fileName = imageItem.FileName;

                    if (annotationsByImage.TryGetValue(fileName, out var cocoAnnotations))
                    {
                        var annotationItems = new List<AnnotationItem>();

                        foreach (var cocoAnnotation in cocoAnnotations)
                        {
                            if (!categoryIdMap.ContainsKey(cocoAnnotation.CategoryId))
                                continue;

                            var className = categoryIdMap[cocoAnnotation.CategoryId];
                            var annotationItem = ConvertCOCOAnnotationToInternal(cocoAnnotation, className);

                            if (annotationItem != null)
                            {
                                annotationItems.Add(annotationItem);
                            }
                        }

                        AllImageAnnotations[fileName] = annotationItems;
                    }

                    processedCount++;
                    Dispatcher.UIThread.InvokeAsync(() => ProgressValue = processedCount);
                }

                // 重新加载当前图像的标注
                Dispatcher.UIThread.Invoke(() =>
                {
                    LoadAnnotationsForCurrentImage();
                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"解析COCO文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重新处理COCO标注数据（用于模式切换）
        /// </summary>
        private void ReprocessAnnotationsFromCOCOData()
        {
            if (_lastImportedCocoDataset == null)
            {
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Orange.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Warning")
                    .HasMessage("没有已导入的COCO标注数据")
                    .Dismiss().WithDelay(3000, t => { })
                    .Queue();
                return;
            }

            try
            {
                // 创建图像ID到文件名的映射
                var imageIdMap = new Dictionary<int, string>();
                var categoryIdMap = new Dictionary<int, string>();

                // 重新构建映射
                foreach (var image in _lastImportedCocoDataset.Images)
                {
                    imageIdMap[image.Id] = image.FileName;
                }

                foreach (var category in _lastImportedCocoDataset.Categories)
                {
                    categoryIdMap[category.Id] = category.Name;
                }

                // 按图像分组标注
                var annotationsByImage = new Dictionary<string, List<Annotation>>();
                foreach (var annotation in _lastImportedCocoDataset.Annotations)
                {
                    if (!imageIdMap.ContainsKey(annotation.ImageId))
                        continue;

                    var fileName = imageIdMap[annotation.ImageId];
                    if (!annotationsByImage.ContainsKey(fileName))
                    {
                        annotationsByImage[fileName] = new List<Annotation>();
                    }
                    annotationsByImage[fileName].Add(annotation);
                }

                AllImageAnnotations.Clear();

                // 按新模式重新处理每个图像的标注
                foreach (var imageItem in ImageList)
                {
                    var fileName = imageItem.FileName;

                    if (annotationsByImage.TryGetValue(fileName, out var cocoAnnotations))
                    {
                        var annotationItems = new List<AnnotationItem>();

                        foreach (var cocoAnnotation in cocoAnnotations)
                        {
                            if (!categoryIdMap.ContainsKey(cocoAnnotation.CategoryId))
                                continue;

                            var className = categoryIdMap[cocoAnnotation.CategoryId];
                            var annotationItem = ConvertCOCOAnnotationToInternal(cocoAnnotation, className);

                            if (annotationItem != null)
                            {
                                annotationItems.Add(annotationItem);
                            }
                        }

                        AllImageAnnotations[fileName] = annotationItems;
                    }
                }

                // 重新加载当前图像的标注
                Dispatcher.UIThread.Invoke(() =>
                {
                    LoadAnnotationsForCurrentImage();
                    UpdateStatistics();
                });

                NotifyManager.CreateMessage()
                    .Accent("#161616")
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Success")
                    .HasMessage($"已按{GetModeDescription()}重新处理标注")
                    .Dismiss().WithDelay(3000, t => { })
                    .Queue();
            }
            catch (Exception ex)
            {
                NotifyManager.CreateMessage()
                    .Accent(Avalonia.Media.Brushes.Red.Color.ToString())
                    .Background("#e5e4e2")
                    .Foreground(Avalonia.Media.Brushes.Black.Color.ToString())
                    .HasBadge("Error")
                    .HasMessage($"重新处理标注失败: {ex.Message}")
                    .Dismiss().WithDelay(5000, t => { })
                    .Queue();
            }
        }

        /// <summary>
        /// 获取当前模式的描述
        /// </summary>
        /// <returns>模式描述文本</returns>
        private string GetModeDescription()
        {
            return IsCheckDetectionRectType ? "识别框标签模式" : "实例分割标签模式";
        }

        /// <summary>
        /// 将COCO标注转换为内部标注模型
        /// </summary>
        /// <param name="cocoAnnotation">COCO标注对象</param>
        /// <param name="className">类别名称</param>
        /// <returns>内部标注模型</returns>
        private AnnotationItem? ConvertCOCOAnnotationToInternal(Annotation cocoAnnotation, string className)
        {
            try
            {
                AnnotationItem annotationItem;

                if (IsCheckDetectionRectType)
                {
                    // 使用检测框模式：使用bbox数据
                    if (cocoAnnotation.Bbox == null || cocoAnnotation.Bbox.Count < 4)
                        return null;

                    var x = cocoAnnotation.Bbox[0];
                    var y = cocoAnnotation.Bbox[1];
                    var width = cocoAnnotation.Bbox[2];
                    var height = cocoAnnotation.Bbox[3];

                    annotationItem = new RectangleModel
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        ClassName = className
                    };
                }
                else
                {
                    // 使用实例分割模式：仅使用segmentation数据
                    if (cocoAnnotation.Segmentation?.Polygons == null || cocoAnnotation.Segmentation.Polygons.Count == 0)
                    {
                        // 实例分割模式下，没有分割数据则跳过此标注
                        return null;
                    }

                    // 使用第一个多边形
                    var polygonData = cocoAnnotation.Segmentation.Polygons[0];
                    if (polygonData.Count < 6 || polygonData.Count % 2 != 0)
                    {
                        // 多边形数据无效，跳过此标注
                        return null;
                    }

                    // 创建多边形标注
                    var points = new ObservableCollection<Avalonia.Point>();
                    for (int i = 0; i < polygonData.Count; i += 2)
                    {
                        points.Add(new Avalonia.Point(polygonData[i], polygonData[i + 1]));
                    }

                    annotationItem = new PolygonModel
                    {
                        Points = [..points],
                        ClassName = className
                    };
                }

                return annotationItem;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"转换COCO标注失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算多边形面积
        /// </summary>
        /// <param name="points">多边形顶点集合</param>
        /// <returns>多边形面积</returns>
        private static double CalculatePolygonArea(ObservableCollection<Avalonia.Point> points)
        {
            if (points == null || points.Count < 3)
                return 0;

            double area = 0;
            int n = points.Count;

            // 用鞋带公式计算多边形面积
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area / 2.0);
        }
        #endregion
    }
}