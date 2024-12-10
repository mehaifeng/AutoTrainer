using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public static class CmdHelper
    {
        /// <summary>
        /// 验证Venv环境是否可用
        /// </summary>
        /// <param name="venvPath"></param>
        /// <returns></returns>
        public static bool IsVenvValid(string venvPath)
        {
            // 构建Python解释器的路径
            string pythonPath = System.IO.Path.Combine(venvPath, "Scripts", "python.exe");

            // 检查Python解释器是否存在
            if (!System.IO.File.Exists(pythonPath))
            {
                return false;
            }

            // 使用Process类执行Python命令
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "-c \"import sys; print(sys.version_info[:2])\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // 读取输出
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                string pattern = @"^\(\d+,\s*\d+\)$";
                //检查输出是否包含Python版本信息
                if (Regex.IsMatch(output.Trim(), pattern))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 执行一条指令 具有返回的方法
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public async static Task<(int, string, string)> ExecuteLine(string fileName, string arguments)
        {
            // 创建进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            return await Task.Factory.StartNew(() =>
            {
                // 启动进程
                using (Process process = Process.Start(startInfo))
                {
                    // 读取输出
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // 等待进程完成
                    process.WaitForExit();

                    return (process.ExitCode, output, error);
                }
            });

        }
        /// <summary>
        /// 执行多条指令 具有返回的方法
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public async static Task<(int, string)> ExecuteMultiLines(string fileName, List<string> commands)
        {
            // 创建一个新的ProcessStartInfo对象
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,  // 也重定向错误输出
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // 创建输出缓冲区
            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = startInfo;

                // 异步处理输出
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                // 异步处理错误
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        error.AppendLine(e.Data);
                    }
                };

                // 启动进程
                process.Start();

                // 开始异步读取
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 写入命令
                using (var writer = process.StandardInput)
                {
                    if (writer.BaseStream.CanWrite)
                    {
                        foreach (var command in commands)
                        {
                            await writer.WriteLineAsync(command);
                        }
                        await writer.FlushAsync();
                        writer.Close(); // 重要：关闭标准输入流
                    }
                }

                // 等待进程结束
                await process.WaitForExitAsync();

                // 返回结果
                return (process.ExitCode, output.ToString() + error.ToString());
            }
        }
        /// <summary>
        /// 打开终端，执行命令,无返回
        /// </summary>
        /// <param name="command">命令</param>
        /// <param name="isShowTerminal">是否显示终端</param>
        /// <returns></returns>
        public async static Task ExecuteCmdWindow(string command, bool isShowTerminal)

        {
            // 创建进程启动信息
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/c " + command;

            // 关键修改：设置进程输出配置
            startInfo.RedirectStandardInput = false;   // 改为false，允许直接输入
            startInfo.RedirectStandardOutput = false;  // 改为false，允许直接输出到控制台
            startInfo.RedirectStandardError = false;   // 改为false，允许直接输出错误到控制台
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = !isShowTerminal;          // 显示控制台窗口

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    // 启动进程
                    using (Process process = Process.Start(startInfo))
                    {
                        // 等待进程结束
                        process.WaitForExit();
                        Console.WriteLine($"\n命令执行完成，退出代码: {process.ExitCode}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行命令时出错: {ex.Message}");
            }
        }
    }
}
