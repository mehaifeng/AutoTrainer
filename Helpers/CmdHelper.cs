using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public static class CmdHelper
    {
        public class CommandResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
        /// <summary>
        /// 验证Venv环境是否可用
        /// </summary>
        /// <param name="venvPath"></param>
        /// <returns></returns>
        public static bool IsVenvValid(string venvPath)
        {
            // 构建Python解释器的路径
            var pythonPath = OperatingSystem.IsWindows()
                ? Path.Combine(venvPath, "Scripts", "python.exe")
                : Path.Combine(venvPath, "bin", "python");

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

        public delegate void OutputReceivedHandler(string data);
    public static async Task<CommandResult> ExecuteLine(string arguments, string? workingDirectory = null, bool isShowTerminal = false, OutputReceivedHandler? onOutputReceived = null)
    {
            var result = new CommandResult();
            // 根据操作系统确定shell程序路径和参数格式
            string shellPath;
            string shellArgs;
            if (OperatingSystem.IsWindows())
            {
                shellPath = "cmd.exe";
                shellArgs = "/c" + arguments;
            }
            else
            {
                // MacOS 和 Linux 都使用 bash
                if (OperatingSystem.IsMacOS())
                {
                    // 在 MacOS 上，bash 一般位于 /bin/bash
                    // 可以通过 which bash 命令找到实际位置
                    shellPath = "/bin/bash";
                    // 某些 MacOS 版本可能默认使用 zsh
                    if (!File.Exists(shellPath))
                    {
                        shellPath = "/bin/zsh";
                    }
                }
                else // Linux
                {
                    shellPath = "/bin/bash";
                }
                // Unix-like 系统的命令参数格式相同
                shellArgs = $"-c \"{arguments.Replace("\"", "\\\"")}\"";
            }
            // 创建进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = shellArgs,
                RedirectStandardOutput = !isShowTerminal,
                RedirectStandardError = !isShowTerminal,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = !isShowTerminal
            };
            if (!isShowTerminal)
            {
                // 设置控制台输出编码为UTF8
                startInfo.StandardOutputEncoding = new UTF8Encoding(false);
                startInfo.StandardErrorEncoding = new UTF8Encoding(false);
            }
            // 添加环境变量以确保正确的编码
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            // 为 Unix-like 系统（MacOS 和 Linux）添加额外的环境变量
            if (!OperatingSystem.IsWindows())
            {
                startInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
                startInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
                // 在 MacOS 上设置 PATH 以确保能找到所需的命令
                if (OperatingSystem.IsMacOS())
                {
                    string defaultPath = "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
                    startInfo.EnvironmentVariables["PATH"] = 
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATH"))
                        ? defaultPath
                        : Environment.GetEnvironmentVariable("PATH") + ":" + defaultPath;
                }
            }
            // 启动进程
            using var process = new Process { StartInfo = startInfo };
            // 如果不显示终端，则捕获输出
            if (!isShowTerminal && onOutputReceived != null)
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        onOutputReceived?.Invoke(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        onOutputReceived?.Invoke($"{args.Data}");
                    }
                };
            }
            process.Start();
            // 如果不显示终端，开始异步读取输出
            if (!isShowTerminal && onOutputReceived != null)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            // 等待进程完成
            await process.WaitForExitAsync();
            // 如果显示终端，不需要读取输出
            if (onOutputReceived == null)
            {
                // 读取输出
                result.Output = await process.StandardOutput.ReadToEndAsync();
                result.Error = await process.StandardError.ReadToEndAsync();
            }
            result.ExitCode = process.ExitCode;
            return result;
        }
        /// <summary>
        /// 执行Python脚本并处理长输出，带有虚拟环境
        /// </summary>
        /// <param name="pythonScriptPath">py脚本地址</param>
        /// <param name="venvPath">虚拟环境地址</param>
        /// <param name="arguments">py脚本参数</param>
        /// <param name="isShowTerminal">是否显示终端</param>
        /// <param name="onOutputReceived">委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public static async Task<CommandResult> ExecutePythonScriptAsync(string pythonScriptPath, string venvPath, string? arguments = null, bool isShowTerminal = false, OutputReceivedHandler? onOutputReceived = null, CancellationToken cancellationToken = default)
        {
            var command = new StringBuilder();
            var activateScript = OperatingSystem.IsWindows()? Path.Combine(venvPath,"Scripts","activate.bat") : $"source {Path.Combine(venvPath, "bin","activate")}";
            command.Append(activateScript);
            //command.Append(" && ");
            //command.Append("set PYTHONIOENCODING=utf-8");
            command.Append(" && ");
            command.Append($"python {pythonScriptPath}");

            if (!string.IsNullOrEmpty(arguments))
            {
                command.Append($" {arguments}");
            }

            return await ExecuteCommandAsync(
                command.ToString(),
                isShowTerminal,
                onOutputReceived: onOutputReceived,
                cancellationToken: cancellationToken);
        }
        /// <summary>
        /// 执行命令行命令并返回详细结果，支持长输出处理
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="isShowTerminal">是否显示终端窗口</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="onOutputReceived">输出接收事件处理器（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>命令执行结果</returns>
        private static async Task<CommandResult> ExecuteCommandAsync(string command,bool isShowTerminal = false,string? workingDirectory = null,OutputReceivedHandler? onOutputReceived = null,CancellationToken cancellationToken = default)
        {
            var result = new CommandResult();
            // 首先设置代码页为 UTF-8
            //var encodingCommand = "chcp 65001 && " + command;
            var encodingCommand = command;
            // 根据操作系统确定shell程序路径和参数格式
            string shellPath;
            string shellArgs;
            if (OperatingSystem.IsWindows())
            {
                shellPath = "cmd.exe";
                shellArgs = "/c" + encodingCommand;
            }
            else
            {
                // MacOS 和 Linux 都使用 bash
                if (OperatingSystem.IsMacOS())
                {
                    // 在 MacOS 上，bash 一般位于 /bin/bash
                    // 可以通过 which bash 命令找到实际位置
                    shellPath = "/bin/bash";
                    // 某些 MacOS 版本可能默认使用 zsh
                    if (!File.Exists(shellPath))
                    {
                        shellPath = "/bin/zsh";
                    }
                }
                else // Linux
                {
                    shellPath = "/bin/bash";
                }
                // Unix-like 系统的命令参数格式相同
                shellArgs = $"-c \"{encodingCommand.Replace("\"", "\\\"")}\"";
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = shellArgs,
                UseShellExecute = isShowTerminal,
                CreateNoWindow = !isShowTerminal,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = !isShowTerminal,
                RedirectStandardError = !isShowTerminal,
                RedirectStandardInput = false,
            };
            // 只有在重定向输出时才设置编码
            if (!isShowTerminal)
            {
                startInfo.StandardOutputEncoding = new UTF8Encoding(false);
                startInfo.StandardErrorEncoding = new UTF8Encoding(false);
            }
            // 添加环境变量以确保正确的编码
            // startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            try
            {
                // 启动进程
                using var process = new Process { StartInfo = startInfo };

                // 如果不显示终端，则捕获输出
                if (!isShowTerminal)
                {
                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            // 触发事件（如果有订阅）
                            onOutputReceived?.Invoke(args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            onOutputReceived?.Invoke($"{args.Data}");
                        }
                    };
                }

                // 启动进程
                process.Start();

                // 如果不显示终端，开始异步读取输出
                if (!isShowTerminal && onOutputReceived != null)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                // 等待进程完成或取消
                await Task.WhenAny(
                    process.WaitForExitAsync(cancellationToken),
                    Task.Delay(Timeout.Infinite, cancellationToken)
                );

                // 如果取消了，则结束进程
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch (InvalidOperationException) { }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                // 获取结果
                result.ExitCode = process.ExitCode;

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"执行命令时出错: {ex.Message}", ex);
            }
        }
    }
}
