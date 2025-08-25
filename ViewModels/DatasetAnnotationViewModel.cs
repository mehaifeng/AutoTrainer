using AutoTrainer.ControlHelper;
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
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using Newtonsoft.Json;
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
        public Canvas ImageCanvas;
        // 操作历史（撤销重做）
        private Stack<List<AnnotationItem>> undoStack = new();
        private Stack<List<AnnotationItem>> redoStack = new();
        // 导航状态
        public bool CanGoPrevious => CurrentImageIndex > 0;
        public bool CanGoNext => CurrentImageIndex < ImageList.Count - 1;
        // 是否有选中的标注
        public bool HasSelectedAnnotation => SelectedAnnotation != null;
        // 图片文件夹
        public string Imagefolder = string.Empty;
        // 标注文件名
        public string AnnotationFileName = "AnnotationConfig.json";
        // 存储所有图片的标注数据
        public Dictionary<string, List<AnnotationItem>> AllImageAnnotations = [];

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
        public Point? drawingStartPoint = null;

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
        #endregion

        #region 事件和委托
        public Action<AnnotationItem>? OnAnnotationSelected;
        #endregion

        #region 构造函数

        public DatasetAnnotationViewModel(Canvas canvas)
        {
            ImageCanvas = canvas;
            // 初始化默认类别
            ClassNames.Add("默认类别");
            SelectedClassName = ClassNames.FirstOrDefault() ?? string.Empty;
            OnAnnotationSelected += SelectedAnnvationChanged;
            // 监听属性变化
            PropertyChanged += DatasetAnnotationViewModel_PropertyChanged;
        }

        private void DatasetAnnotationViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentImageIndex):
                    OnPropertyChanged(nameof(CanGoPrevious));
                    OnPropertyChanged(nameof(CanGoNext));
                    LoadCurrentImage();
                    break;
                case nameof(SelectedAnnotation):
                    OnPropertyChanged(nameof(HasSelectedAnnotation));
                    break;
            }
        }

        #endregion

        #region 标注模式切换命令

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

        #endregion

        #region 标注工具命令

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

        #endregion

        #region 图像导入和导航命令
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
                    await LoadImagesFromFolder(selectedFolder);
                }
            }
            catch (Exception ex)
            {
                // TODO: 显示错误消息
                Debug.WriteLine($"导入目录图像失败: {ex.Message}");
                // 可以考虑添加用户通知，例如：
                // await ShowErrorDialog("导入失败", $"无法导入图像目录：{ex.Message}");
            }
        }

        /// <summary>
        /// 从文件夹加载图像文件
        /// </summary>
        /// <param name="folder"></param>
        private async Task LoadImagesFromFolder(IStorageFolder folder)
        {
            try
            {
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".tif"
                };

                // 清空现有列表（可选，根据需求决定是否保留原有图像）
                ImageList.Clear();

                var files = folder.GetItemsAsync();
                var imageFiles = new List<IStorageFile>();
                await foreach (var item in files)
                {
                    if (item is IStorageFile file &&
                        supportedExtensions.Contains(System.IO.Path.GetExtension(file.Name)))
                    {
                        imageFiles.Add(file);
                    }
                }

                // 按文件名排序
                imageFiles = imageFiles.OrderBy(file => file.Name).ToList();

                var totalFiles = imageFiles.Count;
                var processedFiles = 0;

                var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
                var tasks = imageFiles.Select(async file =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var imageItem = await CreateImageItem(file);
                        if (imageItem != null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ImageList.Add(imageItem);
                            });
                        }

                        var current = Interlocked.Increment(ref processedFiles);
                        var progress = (int)((double)current / totalFiles * 100);
                        //ImportProgressChanged?.Invoke(this, progress);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
                CurrentImage = new Bitmap(ImageList[0].FilePath);
                CurrentImageIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载图像失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建图像项目
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task<ImageItem?> CreateImageItem(IStorageFile file)
        {
            try
            {
                var imageItem = new ImageItem
                {
                    FilePath = file.Path.LocalPath
                };

                // 创建缩略图
                imageItem.Thumbnail = await CreateThumbnail(file);

                return imageItem;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建图像项失败 {file.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建缩略图
        /// </summary>
        /// <param name="file"></param>
        /// <param name="maxSize"></param>
        /// <returns></returns>
        private async Task<Bitmap?> CreateThumbnail(IStorageFile file, int maxSize = 150)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var originalBitmap = new Bitmap(stream);

                // 计算缩略图尺寸，保持宽高比
                var scale = Math.Min((double)maxSize / originalBitmap.PixelSize.Width,
                                   (double)maxSize / originalBitmap.PixelSize.Height);

                var newWidth = (int)(originalBitmap.PixelSize.Width * scale);
                var newHeight = (int)(originalBitmap.PixelSize.Height * scale);

                // 创建缩略图
                return originalBitmap.CreateScaledBitmap(new PixelSize(newWidth, newHeight));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建缩略图失败 {file.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 导入选择的图片
        /// </summary>
        /// <param name="control"></param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ImportImages(UserControl control)
        {
            try
            {
                FilePickerFileType imageAll = new FilePickerFileType("ImageAll")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"],
                    AppleUniformTypeIdentifiers = ["sample.jpg"],
                    MimeTypes = ["image/*"]
                };
                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                    {
                        Title = "选择一张图片",
                        AllowMultiple = false,
                        FileTypeFilter = [imageAll],
                    });
                    if (file.Count != 0)
                    {
                        CurrentImageFileName = System.IO.Path.GetFileName(file[0].TryGetLocalPath() ?? string.Empty);
                        CurrentImage = new Bitmap(file[0].Path.LocalPath);
                        CurrentImageSize = $"{CurrentImage.PixelSize.Width}x{CurrentImage.PixelSize.Height}";
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: 显示错误消息
                Debug.WriteLine($"导入图像失败: {ex.Message}");
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

        #endregion

        #region 类别管理命令
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

        #endregion

        #region 标注操作命令
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
                if(SelectedAnnotation.AnnotationType == AnnotationToolEnum.Rectangle)
                {
                    // 删除矩形标注
                    var rectangle = (RectangleModel)SelectedAnnotation;
                    if (rectangle != null)
                    {
                        var rectItem = ImageCanvas.Children.FirstOrDefault(t => t is Rectangle rect && rect.Tag?.ToString() == rectangle.InstanceGuid);
                        if (rectItem != null)
                        {
                            ImageCanvas.Children.Remove(rectItem);
                        }
                    }
                }
                else if (SelectedAnnotation.AnnotationType == AnnotationToolEnum.Polygon)
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
                else if (SelectedAnnotation.AnnotationType == AnnotationToolEnum.Point)
                {
                    var pointModel = (PointModel)SelectedAnnotation;
                    // 删除点标注
                    var pointItem = ImageCanvas.Children.FirstOrDefault(t => t is Ellipse ellipse && ellipse.Tag?.ToString() == pointModel.InstanceGuid);
                    if (pointItem != null)
                    {
                        ImageCanvas.Children.Remove(pointItem);
                    }
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
        /// 保存标注数据到本地文件
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SaveAnnotation()
        {
            try
            {
                //在图片目录创建Anotations.json文件 保存每一张图片的标注数据
                if (CurrentImageIndex < 0 || CurrentImageIndex >= ImageList.Count)
                {
                    return;
                }
                var annotationPath = System.IO.Path.Combine(Imagefolder, AnnotationFileName);
                File.Create(annotationPath).Close();
                var jsonStr = JsonConvert.SerializeObject(AllImageAnnotations, Formatting.Indented);
                await File.WriteAllTextAsync(annotationPath,jsonStr);
            }
            catch (Exception ex)
            {
                ProgressState = $"保存失败: {ex.Message}";
            }
        }

        #endregion

        #region 模板标注命令

        [RelayCommand]
        private void CreateTemplate()
        {
            IsCreatingTemplate = true;
            CurrentMode = AnnotationMethodEnum.Template;
            // TODO: 进入模板创建模式
        }

        [RelayCommand]
        private void SaveTemplate()
        {
            
        }

        [RelayCommand]
        private void ApplyTemplate()
        {
            
        }

        [RelayCommand]
        private void BatchApplyTemplate()
        {
            
        }

        #endregion

        #region 导入导出命令

        [RelayCommand]
        private async Task ImportAnnotations()
        {
            try
            {
                // TODO: 实现标注文件导入
                // 支持YOLO、COCO等格式
            }
            catch (Exception ex)
            {
                Console.WriteLine($"导入标注失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ExportAnnotations()
        {
            try
            {
                // TODO: 实现标注文件导出
                // 支持多种格式导出
            }
            catch (Exception ex)
            {
                Console.WriteLine($"导出标注失败: {ex.Message}");
            }
        }

        #endregion

        #region 生成数据集
        [RelayCommand]
        private void GenerateDataSet()
        {
            if (IsApplyAsTemplate)
            {
               if(CurrentImageAnnotations.Count > 0)
                {

                }
                else
                {

                }
            }
        }
        #endregion

        #region 私有方法
        private void UpdateUI(AnnotationItem annotationItem)
        {
            switch (annotationItem.AnnotationType)
            {
                case AnnotationToolEnum.Rectangle:
                    if (annotationItem is RectangleModel rectModel)
                    {
                        var rect = new Rectangle
                        {
                            Tag = rectModel.InstanceGuid,
                            Stroke = Brushes.Red,
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
                case AnnotationToolEnum.Polygon:
                    if (annotationItem is PolygonModel polygonModel)
                    {
                        var poloygen = new Polygon
                        {
                            Tag = polygonModel.InstanceGuid,
                            StrokeThickness = 1,
                            Stroke = Brushes.Blue,
                            Points = polygonModel.Points
                        };
                        polygonModel.UIElement = poloygen;
                        ImageCanvas.Children.Add(poloygen);
                    }
                    break;
                case AnnotationToolEnum.Point:
                    if (annotationItem is PointModel pointModel)
                    {
                        var point = new Ellipse
                        {
                            Tag = pointModel.InstanceGuid,
                            Width = 4,
                            Height = 4,
                            Stroke = Brushes.DarkGreen,
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
        private async void LoadCurrentImage()
        {
            if (CurrentImageIndex >= 0 && CurrentImageIndex < ImageList.Count)
            {
                var imageItem = ImageList[CurrentImageIndex];
                CurrentImagePath = imageItem.FilePath;
                CurrentImageFileName = System.IO.Path.GetFileName(imageItem.FilePath);
                try
                {
                    CurrentImage = new Bitmap(imageItem.FilePath);
                    CurrentImageSize = $"{CurrentImage.PixelSize.Width}x{CurrentImage.PixelSize.Height}";
                    
                    // 加载该图像的标注数据
                    await LoadAnnotationsForCurrentImage();
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
            SelectedAnnotation = annotation;
        }

        /// <summary>
        /// 从全局字典或本地文件加载当前图像的标注数据
        /// </summary>
        /// <returns></returns>
        private async Task LoadAnnotationsForCurrentImage()
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
                        var jsonStr = await File.ReadAllTextAsync(annotationPath);
                        var allAnnotations = JsonConvert.DeserializeObject<Dictionary<string, List<AnnotationItem>>>(jsonStr);
                        if (allAnnotations != null && allAnnotations.TryGetValue(CurrentImageFileName, out var fileAnnotations))
                        {
                            foreach (var annotation in fileAnnotations)
                            {
                                CurrentImageAnnotations.Add(annotation);
                                UpdateUI(annotation);
                            }
                            // 更新全局字典
                            AllImageAnnotations[CurrentImageFileName] = fileAnnotations;
                            // 标注添加到图像控件上

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

        #endregion
    }
}