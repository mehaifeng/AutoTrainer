using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;

namespace AutoTrainer.Helpers
{
    public static class CliWrapHelper
    {
        public class CommandResult
        {
            public int ExitCode { get; set; }
            public string? Output { get; set; }
            public string? Error { get; set; }
        }

        public delegate void OutputReceivedHandler(string data);

        /// <summary>
        /// 验证Venv环境是否可用
        /// </summary>
        /// <param name="venvPath"></param>
        /// <returns></returns>
        public static async Task<bool> IsVenvValid(string venvPath)
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

            try
            {
                // 使用CliWrap执行Python命令
                var result = await Cli.Wrap(pythonPath)
                    .WithArguments("-c \"import sys; print(sys.version_info[:2])\"")
                    .ExecuteBufferedAsync();

                var output = result.StandardOutput;
                string pattern = @"^\(\d+,\s*\d+\)$";

                // 检查输出是否包含Python版本信息
                if (Regex.IsMatch(output.Trim(), pattern))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// 执行命令行命令并返回详细结果，支持长输出处理
        /// </summary>
        /// <param name="arguments">要执行的命令</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="isShowTerminal">是否显示终端窗口</param>
        /// <param name="onOutputReceived">输出接收事件处理器（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>命令执行结果</returns>
        public static async Task<CommandResult> ExecuteLine(
            string arguments,
            string? workingDirectory = null,
            bool isShowTerminal = false,
            OutputReceivedHandler? onOutputReceived = null)
        {
            var result = new CommandResult();

            try
            {
                // 根据操作系统确定shell程序路径和参数格式
                string shellPath;
                string shellArgs;

                if (OperatingSystem.IsWindows())
                {
                    shellPath = "cmd.exe";
                    shellArgs = $"/c {arguments}";
                }
                else
                {
                    // MacOS 和 Linux 都使用 bash
                    if (OperatingSystem.IsMacOS())
                    {
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

                var environmentVariables = new Dictionary<string, string?>
                {
                    ["PYTHONIOENCODING"] = "utf-8",
                    ["LANG"] = "en_US.UTF-8",
                    ["LC_ALL"] = "en_US.UTF-8"
                };

                // 为 MacOS 设置 PATH
                if (OperatingSystem.IsMacOS())
                {
                    string defaultPath = "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
                    var currentPath = Environment.GetEnvironmentVariable("PATH");
                    environmentVariables["PATH"] = string.IsNullOrEmpty(currentPath) ? defaultPath : currentPath + ":" + defaultPath;
                }

                var command = Cli.Wrap(shellPath)
                    .WithArguments(shellArgs)
                    .WithWorkingDirectory(workingDirectory ?? Environment.CurrentDirectory)
                    .WithEnvironmentVariables(environmentVariables);

                if (isShowTerminal)
                {
                    // 显示终端模式 - 直接执行而不重定向输出
                    var processResult = await command.ExecuteAsync();
                    result.ExitCode = processResult.ExitCode;
                }
                else
                {
                    if (onOutputReceived != null)
                    {
                        // 实时输出模式
                        var commandTask = command.ListenAsync();
                        CommandResult? finalResult = null;

                        await foreach (var cmdEvent in commandTask)
                        {
                            switch (cmdEvent)
                            {
                                case StandardOutputCommandEvent stdOut:
                                    if (!string.IsNullOrEmpty(stdOut.Text))
                                    {
                                        onOutputReceived(stdOut.Text);
                                    }
                                    break;
                                case StandardErrorCommandEvent stdErr:
                                    if (!string.IsNullOrEmpty(stdErr.Text))
                                    {
                                        onOutputReceived(stdErr.Text);
                                    }
                                    break;
                                case ExitedCommandEvent exited:
                                    finalResult = new CommandResult { ExitCode = exited.ExitCode };
                                    break;
                            }
                        }

                        result.ExitCode = finalResult?.ExitCode ?? 0;
                    }
                    else
                    {
                        // 缓冲输出模式
                        var bufferedResult = await command.ExecuteBufferedAsync(Encoding.UTF8);
                        result.Output = bufferedResult.StandardOutput;
                        result.Error = bufferedResult.StandardError;
                        result.ExitCode = bufferedResult.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.ExitCode = -1;
            }

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
        public static async Task<CommandResult> ExecutePythonScriptAsync(
            string pythonScriptPath,
            string venvPath,
            string? arguments = null,
            bool isShowTerminal = false,
            OutputReceivedHandler? onOutputReceived = null,
            CancellationToken cancellationToken = default)
        {
            var command = new StringBuilder();
            var activateScript = OperatingSystem.IsWindows()
                ? Path.Combine(venvPath, "Scripts", "activate.bat")
                : $"source {Path.Combine(venvPath, "bin", "activate")}";

            command.Append(activateScript);
            command.Append(" && ");
            command.Append($"python {pythonScriptPath}");

            if (!string.IsNullOrEmpty(arguments))
            {
                command.Append($" {arguments}");
            }

            var result = await ExecuteCommandAsync(
                command.ToString(),
                isShowTerminal,
                onOutputReceived: onOutputReceived,
                cancellationToken: cancellationToken);

            return result;
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
        private static async Task<CommandResult> ExecuteCommandAsync(
            string command,
            bool isShowTerminal = false,
            string? workingDirectory = null,
            OutputReceivedHandler? onOutputReceived = null,
            CancellationToken cancellationToken = default)
        {
            var result = new CommandResult();

            try
            {
                // 根据操作系统确定shell程序路径和参数格式
                string shellPath;
                string shellArgs;

                if (OperatingSystem.IsWindows())
                {
                    shellPath = "cmd.exe";
                    shellArgs = $"/c {command}";
                }
                else
                {
                    // MacOS 和 Linux 都使用 bash
                    if (OperatingSystem.IsMacOS())
                    {
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
                    shellArgs = $"-c \"{command.Replace("\"", "\\\"")}\"";
                }

                var environmentVariables = new Dictionary<string, string?>
                {
                    ["PYTHONIOENCODING"] = "utf-8",
                    ["LANG"] = "en_US.UTF-8",
                    ["LC_ALL"] = "en_US.UTF-8"
                };

                // 为 MacOS 设置 PATH
                if (OperatingSystem.IsMacOS())
                {
                    string defaultPath = "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
                    var currentPath = Environment.GetEnvironmentVariable("PATH");
                    environmentVariables["PATH"] = string.IsNullOrEmpty(currentPath) ? defaultPath : currentPath + ":" + defaultPath;
                }

                var cliCommand = Cli.Wrap(shellPath)
                    .WithArguments(shellArgs)
                    .WithWorkingDirectory(workingDirectory ?? Environment.CurrentDirectory)
                    .WithEnvironmentVariables(environmentVariables);

                if (isShowTerminal)
                {
                    // 显示终端模式
                    var processResult = await cliCommand.ExecuteAsync(cancellationToken);
                    result.ExitCode = processResult.ExitCode;
                }
                else
                {
                    if (onOutputReceived != null)
                    {
                        // 实时输出模式
                        var commandTask = cliCommand.ListenAsync();
                        CommandResult? finalResult = null;

                        await foreach (var cmdEvent in commandTask)
                        {
                            switch (cmdEvent)
                            {
                                case StandardOutputCommandEvent stdOut:
                                    if (!string.IsNullOrEmpty(stdOut.Text))
                                    {
                                        onOutputReceived(stdOut.Text);
                                    }
                                    break;
                                case StandardErrorCommandEvent stdErr:
                                    if (!string.IsNullOrEmpty(stdErr.Text))
                                    {
                                        onOutputReceived(stdErr.Text);
                                    }
                                    break;
                                case ExitedCommandEvent exited:
                                    finalResult = new CommandResult { ExitCode = exited.ExitCode };
                                    break;
                            }
                        }

                        result.ExitCode = finalResult?.ExitCode ?? 0;
                    }
                    else
                    {
                        // 缓冲输出模式
                        var bufferedResult = await cliCommand.ExecuteBufferedAsync(Encoding.UTF8, cancellationToken);
                        result.Output = bufferedResult.StandardOutput;
                        result.Error = bufferedResult.StandardError;
                        result.ExitCode = bufferedResult.ExitCode;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.ExitCode = -1;
            }

            return result;
        }
    }
}