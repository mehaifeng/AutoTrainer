using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            try
            {
                bool isFile = File.Exists(path);
                bool isDirectory = Directory.Exists(path);

                if (!isFile && !isDirectory)
                {
                    Console.WriteLine($"路径不存在: {path}");
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (isFile && selectFile)
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (isFile && !selectFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        Process.Start("explorer.exe", path);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (isFile && selectFile)
                    {
                        Process.Start("open", $"-R \"{path}\"");
                    }
                    else if (isFile && !selectFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Process.Start("open", directory);
                    }
                    else
                    {
                        Process.Start("open", path);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (isFile)
                    {
                        var directory = Path.GetDirectoryName(path);
                        Process.Start("xdg-open", directory);
                    }
                    else
                    {
                        Process.Start("xdg-open", path);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法打开路径: {ex.Message}");
            }
        }
    }
}
