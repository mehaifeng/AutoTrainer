using AutoTrainer.Helpers;
using AutoTrainer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Minio.DataModel.Args;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ModelStorehouseViewModel:ViewModelBase
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
            ModelTable = [];
        }
        #region 可绑定属性
        [ObservableProperty]
        private ObservableCollection<string> projectCollection;
        [ObservableProperty]
        private string selectedProject;
        [ObservableProperty]
        private ObservableCollection<ModelFactoryModel> modelTable;
        [ObservableProperty]
        private string minioPath;
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
                if(string.Equals(selectObj.ModelType, "folder"))
                {
                    ModelTable = [];
                    MinioPath = MinioPath + selectObj.ModelName + "/";
                    await GetObjects(SelectedProject, MinioPath.Replace(SelectedProject+'/',string.Empty), false);
                }
            }
        }
        [RelayCommand]
        public async Task BackFolder()
        {
            //返回上一级，当根目录是存储桶时，返回存储桶列表，注意prefix参数不包含桶名称
            if (MinioPath != SelectedProject + "/")
            {
                ModelTable = [];
                var path = MinioPath.Replace(SelectedProject+'/',string.Empty).TrimEnd('/');
                var index = path.LastIndexOf("/");
                path = path.Substring(0, index+1);
                await GetObjects(SelectedProject, path, false);
                MinioPath = MinioPath.Substring(0, MinioPath.TrimEnd('/').LastIndexOf('/')+1);
            }
        }
        #endregion
        /// <summary>
        /// 获取桶中一级的文件和目录
        /// </summary>
        /// <param name="bucketName"></param>
        /// <returns></returns>
        private async Task GetObjects(string bucketName,string? prefix =null, bool recursive = false)
        {
            var objs = await MinIOStorageHelper.ListFilesAndDirectoriesAsync(bucketName,prefix,recursive);
            foreach(var obj in objs.Files)
            {
                ModelFactoryModel model = new()
                {
                    ModelName = obj,
                    ModelType = obj.Split('.')[1]
                };
                ModelTable.Add(model);
            }
            foreach(var obj in objs.Directories)
            {
                ModelFactoryModel model = new()
                {
                    ModelName = obj,
                    ModelType = "folder"
                };
                ModelTable.Add(model);
            }
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
                MinioPath = SelectedProject + "/";
                await GetObjects(SelectedProject,null,false);
            }
        }
    }
}
