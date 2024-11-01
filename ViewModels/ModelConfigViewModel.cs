﻿using AutoTrainer.CmdHelper;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.ViewModels
{
    public partial class ModelConfigViewModel : ViewModelBase
    {
        #region 构造函数
        public ModelConfigViewModel()
        {
            ModelList = [];
            Requirements = string.Join("\r\n", RequireApps);
            Task.Run(GetPython);
        }
        #endregion

        #region 全局属性
        private string[] RequireApps = ["torch", "torchvision", "opencv-python", "pillow", "Matplotlib", "scikit-learn", "albumentations", "tqdm"];
        private List<string> MissingApps = [];
        StringBuilder sb = new StringBuilder();
        #endregion

        #region 绑定属性
        [ObservableProperty]
        private string pythonPath;
        [ObservableProperty]
        private string pythonVenvPath;
        [ObservableProperty]
        private string pipApps;
        [ObservableProperty]
        private string requirements;
        [ObservableProperty]
        private string enviromentState = "Environment State";
        [ObservableProperty]
        private IBrush stateForeground = Brushes.Green;
        [ObservableProperty]
        private ObservableCollection<string> modelList;
        [ObservableProperty]
        private string selectModel;
        [ObservableProperty]
        private string selectModelIntroduce;
        [ObservableProperty]
        private bool isLoadingModelList = false;
        [ObservableProperty]
        private bool isVisibleIntroduce;
        [ObservableProperty]
        private string outputs;
        [ObservableProperty]
        private bool isCheckedGlobal;
        [ObservableProperty]
        private bool isCheckedVenv = true;
        [ObservableProperty]
        private bool isVisibleInstallMissing = false;
        [ObservableProperty]
        private bool isVisibleProgressBar = false;
        [ObservableProperty]
        private bool isRunningProgressBar = false;
        #endregion

        #region 函数
        /// <summary>
        /// 找到Python
        /// </summary>
        private async Task GetPython()
        {
            string command = "/c where python";
            string fileName = "cmd.exe";
            (int, string, string) result = await CmdHelper.CmdHelper.ExecuteLine(fileName, command);
            if (result.Item1 == 0)
            {
                if (!string.IsNullOrEmpty(result.Item2))
                {
                    var paths = result.Item2.Split("\r\n");
                    PythonPath = paths[0];
                }
            }
            sb.Append(result.Item2);
            sb.Append(result.Item3);
            Outputs = sb.ToString();
        }
        /// <summary>
        /// 获取模型列表
        /// </summary>
        /// <returns></returns>
        private async Task GetModels()
        {
            if (!string.IsNullOrEmpty(PythonVenvPath))
            {
                List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", $"python {AppDomain.CurrentDomain.BaseDirectory}PyScripts\\ModelHelper.py list", $"{PythonVenvPath}\\Scripts\\deactivate.bat"];
                var result = await CmdHelper.CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                if (result.Item1 == 0)
                {
                    if (result.Item2.Contains("###Models###"))
                    {
                        var modelNames = result.Item2.Split("###Models###")[1].TrimStart().TrimEnd().Split("\r\n");
                        ModelList = new ObservableCollection<string>(modelNames);
                    }
                }
            }
        }
        #endregion

        #region 命令
        /// <summary>
        /// 进入Venv环境，执行Pip List
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task ExecutePy()
        {
            if (IsCheckedVenv && !string.IsNullOrEmpty(PythonVenvPath))
            {
                //验证所填venv环境是否可用
                bool isVenvValid = CmdHelper.CmdHelper.IsVenvValid(PythonVenvPath);
                if (isVenvValid)
                {
                    List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", "pip list"];
                    (int, string) result = await CmdHelper.CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                    if (result.Item1 == 0)
                    {
                        string envName = $"({PythonVenvPath.Split("\\").Last()})";
                        PipApps = result.Item2.Split("pip list")[1].Split(envName)[0].TrimStart().TrimEnd();
                    }
                    sb.Append(result.Item2);
                    Outputs = sb.ToString();
                    //判断pip软件包是否包含了要求的软件包
                    var apps = PipApps.Split("\r\n");
                    bool isMatch = true;
                    foreach (var require in RequireApps)
                    {
                        for (int i = 0; i < apps.Length; i++)
                        {
                            if (apps[i].ToLower().Contains(require.ToLower()))
                            {
                                break;
                            }
                            if (!apps[i].ToLower().Contains(require.ToLower()) && i == apps.Length - 1)
                            {
                                sb.AppendLine($"require {require}, but not install");
                                MissingApps.Add(require);
                                isMatch = false;
                            }
                        }
                    }
                    if (!isMatch)
                    {
                        EnviromentState = "Environment State: No Match";
                        StateForeground = Brushes.Red;
                        IsVisibleInstallMissing = true;
                    }
                    else
                    {
                        await GetModels();
                        EnviromentState = "Environment State: All Match";
                        StateForeground = Brushes.Green;
                        IsVisibleInstallMissing = false;
                    }
                    Outputs = sb.ToString();
                }
                else
                {
                    sb.AppendLine("Venv path is unavailable");
                    Outputs = sb.ToString();
                }
            }
            else
            {
                sb.Append("Global enviroment is not ready");
                Outputs = sb.ToString();
            }
        }
        /// <summary>
        /// 安装需要的软件包
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task InstallMissingApp()
        {
            if (MissingApps.Count > 0)
            {
                StringBuilder commands = new StringBuilder();
                commands.Append($"{PythonVenvPath}\\Scripts\\activate.bat");
                foreach (var missingApp in MissingApps)
                {
                    commands.Append($"&& pip install {missingApp}");
                }

                try
                {
                    // 设置进度条状态
                    IsVisibleProgressBar = true;
                    IsRunningProgressBar = true;


                    // 创建并等待所有任务完成
                    Task[] commonTasks = [CmdHelper.CmdHelper.ExecuteCmdWindow(commands.ToString()), ExecutePy()];

                    // 等待所有任务完成
                    await Task.WhenAll(commonTasks);
                }
                finally
                {
                    // 在任务完成或发生异常时都能重置进度条状态
                    IsVisibleProgressBar = false;
                    IsRunningProgressBar = false;
                }
            }
        }
        /// <summary>
        /// 选择模型命令
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task SelectModelToShow()
        {
            if (!string.IsNullOrEmpty(SelectModel))
            {
                IsLoadingModelList = true;
                List<string> commands = [$"{PythonVenvPath}\\Scripts\\activate.bat", $"python {AppDomain.CurrentDomain.BaseDirectory}PyScripts\\ModelHelper.py info {SelectModel}", $"{PythonVenvPath}\\Scripts\\deactivate.bat"];
                var result = await CmdHelper.CmdHelper.ExecuteMultiLines("cmd.exe", commands);
                IsLoadingModelList = false;
                if (result.Item1 == 0)
                {
                    if (result.Item2.Contains("###ModelInfo###"))
                    {
                        SelectModelIntroduce = result.Item2.Split("###ModelInfo###")[1].TrimStart().TrimEnd();
                        IsVisibleIntroduce = true;
                    }
                }
            }
        }
        #endregion
    }
}