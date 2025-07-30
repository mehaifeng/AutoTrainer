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
    public partial class DatasetAnnotationViewModel : ViewModelBase
    {
        #region 可绑定字段属性

        // 标注模式
        [ObservableProperty]
        private AnnotationMode currentMode = AnnotationMode.Manual;

        // 标注工具
        [ObservableProperty]
        private AnnotationTool currentTool = AnnotationTool.Rectangle;

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

        // 模板标注相关
        [ObservableProperty]
        private ObservableCollection<TemplateConfig> templateConfigs = new();

        [ObservableProperty]
        private TemplateConfig? currentTemplate;

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

        // 操作历史（撤销重做）
        private Stack<List<AnnotationItem>> undoStack = new();
        private Stack<List<AnnotationItem>> redoStack = new();

        [ObservableProperty]
        private bool canUndo = false;

        [ObservableProperty]
        private bool canRedo = false;

        // 导航状态
        public bool CanGoPrevious => CurrentImageIndex > 0;
        public bool CanGoNext => CurrentImageIndex < ImageList.Count - 1;

        // 是否有选中的标注
        public bool HasSelectedAnnotation => SelectedAnnotation != null;

        #endregion

        #region 构造函数

        public DatasetAnnotationViewModel()
        {
            // 初始化默认类别
            ClassNames.Add("默认类别");
            SelectedClassName = ClassNames.FirstOrDefault() ?? string.Empty;

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
            CurrentMode = AnnotationMode.Manual;
            IsCreatingTemplate = false;
        }

        [RelayCommand]
        private void SelectTemplateMode()
        {
            CurrentMode = AnnotationMode.Template;
        }

        [RelayCommand]
        private void SelectAIMode()
        {
            CurrentMode = AnnotationMode.AIAssisted;
            // TODO: 实现AI辅助标注
        }

        #endregion

        #region 标注工具命令

        [RelayCommand]
        private void SelectRectangleTool()
        {
            CurrentTool = AnnotationTool.Rectangle;
        }

        [RelayCommand]
        private void SelectPolygonTool()
        {
            CurrentTool = AnnotationTool.Polygon;
        }

        [RelayCommand]
        private void SelectPointTool()
        {
            CurrentTool = AnnotationTool.Point;
        }

        #endregion

        #region 图像导入和导航命令

        [RelayCommand]
        private async Task ImportImages()
        {
            try
            {
                // TODO: 实现文件选择对话框
            }
            catch (Exception ex)
            {
                // TODO: 显示错误消息
                Console.WriteLine($"导入图像失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void PreviousImage()
        {
            if (CanGoPrevious)
            {
                CurrentImageIndex--;
            }
        }

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

        [RelayCommand]
        private void AddClass()
        {
            if (!string.IsNullOrWhiteSpace(NewClassName) && !ClassNames.Contains(NewClassName))
            {
                ClassNames.Add(NewClassName);
                NewClassName = string.Empty;
            }
        }

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

        [RelayCommand]
        private void ClearAnnotations()
        {
            if (CurrentImageAnnotations.Any())
            {
                SaveToUndoStack();
                CurrentImageAnnotations.Clear();
                UpdateStatistics();
            }
        }

        [RelayCommand]
        private void CopyAnnotations()
        {
            // TODO: 实现复制当前图像的标注到剪贴板或下一张图像
        }

        [RelayCommand]
        private void DeleteSelectedAnnotation()
        {
            if (SelectedAnnotation != null)
            {
                SaveToUndoStack();
                CurrentImageAnnotations.Remove(SelectedAnnotation);
                SelectedAnnotation = null;
                UpdateStatistics();
            }
        }

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

        [RelayCommand]
        private async Task SaveAnnotation()
        {
            try
            {
                // TODO: 保存当前图像的标注到文件
                ProgressState = "保存中...";
                await Task.Delay(1000); // 模拟保存过程
                ProgressState = "已保存";
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
            CurrentMode = AnnotationMode.Template;
            // TODO: 进入模板创建模式
        }

        [RelayCommand]
        private void SaveTemplate()
        {
            if (CurrentImageAnnotations.Any())
            {
                var template = new TemplateConfig
                {
                    Name = $"模板_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Annotations = new List<AnnotationItem>(CurrentImageAnnotations),
                    ImageSize = CurrentImageSize
                };

                TemplateConfigs.Add(template);
                CurrentTemplate = template;
                IsCreatingTemplate = false;
            }
        }

        [RelayCommand]
        private void ApplyTemplate(TemplateConfig template)
        {
            if (template != null)
            {
                SaveToUndoStack();
                CurrentImageAnnotations.Clear();

                foreach (var annotation in template.Annotations)
                {
                    CurrentImageAnnotations.Add(new AnnotationItem
                    {
                        ClassName = annotation.ClassName,
                        X = annotation.X,
                        Y = annotation.Y,
                        Width = annotation.Width,
                        Height = annotation.Height,
                        ToolType = annotation.ToolType
                    });
                }

                UpdateStatistics();
            }
        }

        [RelayCommand]
        private async Task BatchApplyTemplate()
        {
            if (CurrentTemplate == null || !ImageList.Any())
                return;

            try
            {
                IsLoading = true;
                ProgressMax = ImageList.Count;
                ProgressValue = 0;
                ProgressState = "批量应用模板中...";

                for (int i = 0; i < ImageList.Count; i++)
                {
                    CurrentImageIndex = i;
                    await Task.Delay(100); // 给UI更新时间

                    ApplyTemplate(CurrentTemplate);
                    await SaveAnnotation();

                    ProgressValue = i + 1;
                    ProgressState = $"已处理 {ProgressValue}/{ProgressMax} 张图片";
                }

                ProgressState = "批量应用完成";
            }
            catch (Exception ex)
            {
                ProgressState = $"批量应用失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
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

        #region 私有方法

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

        private async Task LoadAnnotationsForCurrentImage()
        {
            // TODO: 从文件或数据库加载标注数据
            CurrentImageAnnotations.Clear();
            UpdateStatistics();
        }

        private void SaveToUndoStack()
        {
            undoStack.Push(new List<AnnotationItem>(CurrentImageAnnotations));
            redoStack.Clear(); // 清空重做栈
            UpdateStackStates();
        }

        private void UpdateStackStates()
        {
            CanUndo = undoStack.Count > 0;
            CanRedo = redoStack.Count > 0;
        }

        private void UpdateStatistics()
        {
            TotalImageCount = ImageList.Count;
            AnnotatedImageCount = ImageList.Count(img => img.IsAnnotated);
            TotalAnnotationCount = ImageList.Sum(img => img.AnnotationCount);
        }

        #endregion
    }

    #region 辅助类和枚举

    public enum AnnotationMode
    {
        Manual,     // 手动标注
        Template,   // 模板标注
        AIAssisted  // AI辅助标注
    }

    public enum AnnotationTool
    {
        Rectangle,  // 矩形框
        Polygon,    // 多边形
        Point       // 点标注
    }

    public partial class ImageItem : ObservableObject
    {
        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private Bitmap? thumbnail;

        [ObservableProperty]
        private bool isSelected = false;

        [ObservableProperty]
        private bool isAnnotated = false;

        [ObservableProperty]
        private int annotationCount = 0;
    }

    public partial class AnnotationItem : ObservableObject
    {
        [ObservableProperty]
        private string className = string.Empty;

        [ObservableProperty]
        private double x = 0;

        [ObservableProperty]
        private double y = 0;

        [ObservableProperty]
        private double width = 0;

        [ObservableProperty]
        private double height = 0;

        [ObservableProperty]
        private AnnotationTool toolType = AnnotationTool.Rectangle;

        [ObservableProperty]
        private bool isVisible = true;

        [ObservableProperty]
        private bool isSelected = false;

        public string BoundsInfo => $"({X:F0}, {Y:F0}) - {Width:F0}x{Height:F0}";
    }

    public partial class TemplateConfig : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private List<AnnotationItem> annotations = new();

        [ObservableProperty]
        private string imageSize = string.Empty;

        [ObservableProperty]
        private DateTime createdTime = DateTime.Now;
    }
    #endregion
}