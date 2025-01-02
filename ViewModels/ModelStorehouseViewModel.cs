using AutoTrainer.Helpers;
using AutoTrainer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ModelStorehouseViewModel : ViewModelBase, INotifyPropertyChanged
    {
        MinIOStorageHelper MinIOStorageHelper { get; set; }
        public static async Task<ModelStorehouseViewModel> CreatAsync()
        {
            var viewmodel = new ModelStorehouseViewModel();
            await viewmodel.InitializeAsync();
            return viewmodel;
        }
        public ModelStorehouseViewModel()
        {
            MinIOStorageHelper = new MinIOStorageHelper(App.endPoint, App.accessKey, App.secretKey);
            ProjectCollection = [];
            CheckedStates = [];
            ModelTable = [];
            WeakReferenceMessenger.Default.Register<ModelFactoryModel,string>(this, "CheckedSingleToken", (r,m)=>CheckedSingle(m));
        }
        public static (int,int) SelectedDivisor { get; set; }

        #region 可绑定属性
        [ObservableProperty]
        private ObservableCollection<string> projectCollection;
        [ObservableProperty]
        private string selectedProject = string.Empty;
        [ObservableProperty]
        private ObservableCollection<bool> checkedStates;
        [ObservableProperty]
        private ObservableCollection<ModelFactoryModel> modelTable;
        [ObservableProperty]
        private bool? isAllChecked = false;
        [ObservableProperty]
        private string minioPath = string.Empty;
        [ObservableProperty]
        private bool isShowDownloadBtn = false;
        [ObservableProperty]
        private bool isShowDownloadState = false;
        public async Task InitializeAsync()
        {
            await GetAllBuckets();
            await LoadFirstBucket();
        }
        /// <summary>
        /// 进入文件夹
        /// </summary>
        /// <param name="selectObj"></param>
        /// <returns></returns>
        [RelayCommand]
        public async Task OpenFolder(ModelFactoryModel selectObj)
        {
            if (selectObj != null)
            {
                if (string.Equals(selectObj.ModelType, "folder"))
                {
                    ModelTable = [];
                    CheckedStates = [];
                    MinioPath = MinioPath + selectObj.ModelName + "/";
                    await GetObjects(SelectedProject, MinioPath.Replace(SelectedProject + '/', string.Empty), false);
                    IsAllChecked = false;
                    IsShowDownloadBtn = false;
                }
            }
        }
        /// <summary>
        /// 退后
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task BackFolder()
        {
            //返回上一级，当根目录是存储桶时，返回存储桶列表，注意prefix参数不包含桶名称
            if (MinioPath != SelectedProject + "/")
            {
                ModelTable = [];
                CheckedStates = [];
                var path = MinioPath.Replace(SelectedProject + '/', string.Empty).TrimEnd('/');
                var index = path.LastIndexOf("/");
                path = path.Substring(0, index + 1);
                await GetObjects(SelectedProject, path, false);
                MinioPath = MinioPath.Substring(0, MinioPath.TrimEnd('/').LastIndexOf('/') + 1);
                IsAllChecked = false;
                IsShowDownloadBtn = false;
            }
        }
        /// <summary>
        /// 选择全部/反选
        /// </summary>
        [RelayCommand]
        public void ExcuteSelectAll()
        {
            if (IsAllChecked.HasValue)
            {
                foreach (var model in ModelTable)
                {
                    model.IsChecked = IsAllChecked.Value;
                }
                CheckedStates = IsAllChecked.Value ? new ObservableCollection<bool>(Enumerable.Repeat(true, ModelTable.Count)) : [];
                IsShowDownloadBtn = IsAllChecked.Value;
            }
        }
        /// <summary>
        /// 下载选择的对象
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task DownloadSelected()
        {
            IsShowDownloadState = true;
            var selectedModels = ModelTable.Where(m => m.IsChecked).ToList();
            var path = string.Empty;
            List<Task> DownloadTasks = [];
            if (selectedModels.Count == 0)
            {
                IsShowDownloadState = false;
                return;
            }
            if (MinioPath.StartsWith(SelectedProject + '/'))
            {
                path = MinioPath.Substring(SelectedProject.Length);
            }
            foreach (var model in selectedModels)
            {
                var objPath = Path.Combine(path,model.ModelName);
                var destinationPath = Path.Combine(App.ObjDownloadPath,model.ModelName);
                Task downloadTask =  MinIOStorageHelper.DownloadFileWithProgressAsync(SelectedProject, objPath, destinationPath, new Progress<double>((percentComplete) =>
                {
                    model.DownloadProgress = (int)percentComplete;
                }));
                DownloadTasks.Add(downloadTask);
                while (DownloadTasks.Count > 0)
                {
                    var completeTask = await Task.WhenAny([..DownloadTasks]);
                    DownloadTasks.Remove(completeTask);
                }
            }
            IsShowDownloadState = false;
        }
        #endregion
        /// <summary>
        /// 子项勾选
        /// </summary>
        /// <param name="model"></param>
        private void CheckedSingle(ModelFactoryModel model)
        {
            if (model != null)
            {
                if (model.IsChecked)
                {
                    CheckedStates.Add(true);
                }
                else
                {
                    CheckedStates.Remove(true);
                }
                if (CheckedStates.Count == ModelTable.Count)
                {
                    IsAllChecked = true;
                    IsShowDownloadBtn = true;
                }
                else if(CheckedStates.Count == 0)
                {
                    IsAllChecked = false;
                    IsShowDownloadBtn = false;
                }
                else
                {
                    IsAllChecked = null;
                    IsShowDownloadBtn = true;
                }
            }
        }
        /// <summary>
        /// 获取桶中一级的文件和目录
        /// </summary>
        /// <param name="bucketName"></param>
        /// <returns></returns>
        private async Task GetObjects(string bucketName, string? prefix = null, bool recursive = false)
        {
            var objs = await MinIOStorageHelper.ListFilesAndDirectoriesAsync(bucketName, prefix, recursive);
            foreach (var obj in objs.Files)
            {
                ModelFactoryModel model = new()
                {
                    IsChecked = false,
                    ModelName = obj.Name,
                    ModelType = obj.Name.Split('.')[1],
                    ModelVersion = obj.VersionId,
                    ModelSize = obj.Size,
                    LastModifiedDateTime = obj.LastModified,
                };
                ModelTable.Add(model);
            }
            foreach (var obj in objs.Directories)
            {
                ModelFactoryModel model = new()
                {
                    IsChecked = false,
                    ModelName = obj,
                    ModelType = "folder"
                };
                ModelTable.Add(model);
            }
            SelectedDivisor = (0, ModelTable.Count);
        }
        /// <summary>
        /// 获取所有存储桶
        /// </summary>
        /// <returns></returns>
        private async Task GetAllBuckets()
        {
            var buckets = await MinIOStorageHelper.ListBucketsAsync();
            if (buckets != null)
            {
                foreach (var bucket in buckets)
                {
                    var bucketName = bucket.Name;
                    ProjectCollection.Add(bucketName);
                }
            }
        }
        /// <summary>
        /// 加载第一个存储桶
        /// </summary>
        /// <returns></returns>
        private async Task LoadFirstBucket()
        {
            if (ProjectCollection.Count > 0)
            {
                SelectedProject = ProjectCollection[0];
                MinIOStorageHelper.bucketName = SelectedProject;
                MinioPath = SelectedProject + "/";
                await GetObjects(SelectedProject, null, false);
            }
        }


    }
}
