using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace AutoTrainer.Helpers
{
    public static class FileDirectoryHelper
    {
        /// <summary>
        /// 在资源管理器中打开指定路径
        /// </summary>
        /// <param name="path">文件或目录路径</param>
        /// <param name="selectFile">如果路径是文件，是否选中该文件（仅适用于文件路径）</param>
        public static void OpenInExplorer(string path, bool selectFile = false)
        {
            Log.Debug("在资源管理器中打开路径: {Path}, 选择文件: {SelectFile}", path, selectFile);

            if (string.IsNullOrEmpty(path))
            {
                Log.Warning("无法在资源管理器中打开空路径");
                return;
            }

            try
            {
                bool isFile = File.Exists(path);
                bool isDirectory = Directory.Exists(path);

                if (!isFile && !isDirectory)
                {
                    Log.Error("路径不存在: {Path}", path);
                    return;
                }

                Log.Debug("路径验证 - 是否为文件: {IsFile}, 是否为目录: {IsDirectory}", isFile, isDirectory);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Log.Debug("在Windows平台上打开路径");
                    if (isFile && selectFile)
                    {
                        Log.Debug("打开并选择文件: {Path}", path);
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (isFile && !selectFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Log.Debug("打开文件所在目录: {Directory}", directory);
                        Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        Log.Debug("打开目录: {Path}", path);
                        Process.Start("explorer.exe", path);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Log.Debug("在macOS平台上打开路径");
                    if (isFile && selectFile)
                    {
                        Log.Debug("在macOS上打开并选择文件: {Path}", path);
                        Process.Start("open", $"-R \"{path}\"");
                    }
                    else if (isFile && !selectFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Log.Debug("在macOS上打开文件所在目录: {Directory}", directory);
                        Process.Start("open", directory);
                    }
                    else
                    {
                        Log.Debug("在macOS上打开目录: {Path}", path);
                        Process.Start("open", path);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Log.Debug("在Linux平台上打开路径");
                    if (isFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Log.Debug("在Linux上打开文件所在目录: {Directory}", directory);
                        Process.Start("xdg-open", directory);
                    }
                    else
                    {
                        Log.Debug("在Linux上打开目录: {Path}", path);
                        Process.Start("xdg-open", path);
                    }
                }
                else
                {
                    Log.Warning("不支持的文件浏览器操作系统: {OS}", RuntimeInformation.OSDescription);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "在资源管理器中打开路径失败: {Path}", path);
                throw;
            }
        }
    }
}
