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
using Serilog;

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
            Log.Debug("验证Python虚拟环境: {VenvPath}", venvPath);

            if (string.IsNullOrEmpty(venvPath))
            {
                Log.Warning("虚拟环境路径为空");
                return false;
            }

            try
            {
                // 构建Python解释器的路径
                var pythonPath = OperatingSystem.IsWindows()
                    ? Path.Combine(venvPath, "Scripts", "python.exe")
                    : Path.Combine(venvPath, "bin", "python");

                Log.Debug("检查Python解释器: {PythonPath}", pythonPath);

                // 检查Python解释器是否存在
                if (!System.IO.File.Exists(pythonPath))
                {
                    Log.Warning("未找到Python解释器: {PythonPath}", pythonPath);
                    return false;
                }

                Log.Debug("找到Python解释器，测试功能");
                // 使用CliWrap执行Python命令
                var result = await Cli.Wrap(pythonPath)
                    .WithArguments("-c \"import sys; print(sys.version_info[:2])\"")
                    .ExecuteBufferedAsync();

                var output = result.StandardOutput;
                string pattern = @"^\(\d+,\s*\d+\)$";

                Log.Debug("Python版本测试输出: {Output}", output.Trim());

                // 检查输出是否包含Python版本信息
                if (Regex.IsMatch(output.Trim(), pattern))
                {
                    Log.Information("虚拟环境验证成功: {VenvPath}", venvPath);
                    return true;
                }
                else
                {
                    Log.Warning("Python版本输出格式无效: {Output}", output.Trim());
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "验证Python虚拟环境失败: {VenvPath}", venvPath);
                return false;
            }
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
            OutputReceivedHandler? onOutputReceived = null,
            bool enableVerboseLogging = true)
        {
            if (enableVerboseLogging)
            {
                Log.Debug("执行命令行: {Arguments} 在目录: {Directory}, 显示终端: {ShowTerminal}",
                    arguments, workingDirectory ?? Environment.CurrentDirectory, isShowTerminal);
            }

            if (string.IsNullOrWhiteSpace(arguments))
            {
                Log.Warning("无法执行空命令");
                return new CommandResult { ExitCode = -1, Error = "Command cannot be empty" };
            }

            var result = new CommandResult();
            var startTime = DateTime.UtcNow;

            try
            {
                // 根据操作系统确定shell程序路径和参数格式
                string shellPath;
                string shellArgs;

                if (OperatingSystem.IsWindows())
                {
                    shellPath = "cmd.exe";
                    shellArgs = $"/c {arguments}";
                    if (enableVerboseLogging)
                    {
                        Log.Debug("使用Windows shell: {ShellPath} 参数: {ShellArgs}", shellPath, shellArgs);
                    }
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
                            if (enableVerboseLogging)
                            {
                                Log.Debug("macOS上未找到bash，使用zsh: {ShellPath}", shellPath);
                            }
                        }
                        else if (enableVerboseLogging)
                        {
                            Log.Debug("使用macOS bash shell: {ShellPath}", shellPath);
                        }
                    }
                    else // Linux
                    {
                        shellPath = "/bin/bash";
                        if (enableVerboseLogging)
                        {
                            Log.Debug("使用Linux bash shell: {ShellPath}", shellPath);
                        }
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
                    if (enableVerboseLogging)
                    {
                        Log.Debug("设置macOS PATH: {Path}", environmentVariables["PATH"]);
                    }
                }

                if (enableVerboseLogging)
                {
                    Log.Debug("配置命令环境变量: {EnvVars}",
                        string.Join(", ", environmentVariables.Keys));
                }

                var command = Cli.Wrap(shellPath)
                    .WithArguments(shellArgs)
                    .WithWorkingDirectory(workingDirectory ?? Environment.CurrentDirectory)
                    .WithEnvironmentVariables(environmentVariables);

                if (isShowTerminal)
                {
                    if (enableVerboseLogging)
                    {
                        Log.Debug("在终端模式下执行命令（无输出重定向）");
                    }
                    // 显示终端模式 - 直接执行而不重定向输出
                    var processResult = await command.ExecuteAsync();
                    result.ExitCode = processResult.ExitCode;
                    if (enableVerboseLogging)
                    {
                        Log.Information("终端模式下命令完成，退出码: {ExitCode}", processResult.ExitCode);
                    }
                }
                else
                {
                    if (onOutputReceived != null)
                    {
                        if (enableVerboseLogging)
                        {
                            Log.Debug("在实时输出模式下执行命令");
                        }
                        // 实时输出模式
                        var commandTask = command.ListenAsync();
                        CommandResult? finalResult = null;
                        var outputLines = 0;
                        var errorLines = 0;

                        await foreach (var cmdEvent in commandTask)
                        {
                            switch (cmdEvent)
                            {
                                case StandardOutputCommandEvent stdOut:
                                    if (!string.IsNullOrEmpty(stdOut.Text))
                                    {
                                        outputLines++;
                                        if (enableVerboseLogging)
                                        {
                                            Log.Debug("命令标准输出行 {LineCount}: {Text}", outputLines,
                                                stdOut.Text.Trim().Substring(0, Math.Min(100, stdOut.Text.Trim().Length)));
                                        }
                                        onOutputReceived(stdOut.Text);
                                    }
                                    break;
                                case StandardErrorCommandEvent stdErr:
                                    if (!string.IsNullOrEmpty(stdErr.Text))
                                    {
                                        errorLines++;
                                        Log.Warning("命令标准错误行 {LineCount}: {Text}", errorLines,
                                            stdErr.Text.Trim().Substring(0, Math.Min(100, stdErr.Text.Trim().Length)));
                                        onOutputReceived(stdErr.Text);
                                    }
                                    break;
                                case ExitedCommandEvent exited:
                                    finalResult = new CommandResult { ExitCode = exited.ExitCode };
                                    if (enableVerboseLogging)
                                    {
                                        Log.Information("命令进程退出，退出码: {ExitCode}", exited.ExitCode);
                                    }
                                    break;
                            }
                        }

                        result.ExitCode = finalResult?.ExitCode ?? 0;
                        if (enableVerboseLogging)
                        {
                            Log.Debug("实时执行完成。总标准输出行数: {StdOutCount}, 标准错误行数: {StdErrCount}",
                                outputLines, errorLines);
                        }
                    }
                    else
                    {
                        if (enableVerboseLogging)
                        {
                            Log.Debug("在缓冲输出模式下执行命令");
                        }
                        // 缓冲输出模式
                        var bufferedResult = await command.ExecuteBufferedAsync(Encoding.UTF8);
                        result.Output = bufferedResult.StandardOutput;
                        result.Error = bufferedResult.StandardError;
                        result.ExitCode = bufferedResult.ExitCode;

                        if (enableVerboseLogging)
                        {
                            Log.Debug("缓冲执行完成。退出码: {ExitCode}, 标准输出长度: {StdOutLen}, 标准错误长度: {StdErrLen}",
                                bufferedResult.ExitCode,
                                bufferedResult.StandardOutput?.Length ?? 0,
                                bufferedResult.StandardError?.Length ?? 0);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("命令执行被取消: {Arguments}", arguments);
                result.Error = "Command execution was cancelled";
                result.ExitCode = -1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "命令执行失败: {Arguments}", arguments);
                result.Error = ex.Message;
                result.ExitCode = -1;
            }
            finally
            {
                if (enableVerboseLogging)
                {
                    var duration = DateTime.UtcNow - startTime;
                    Log.Information("命令执行完成，耗时 {Duration}ms，退出码 {ExitCode}: {Arguments}",
                        duration.TotalMilliseconds, result.ExitCode, arguments);
                }
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
            Log.Information("执行Python脚本: {PythonScript} 虚拟环境: {VenvPath}, 参数: {Arguments}",
                pythonScriptPath, venvPath, arguments ?? "无");

            if (string.IsNullOrEmpty(pythonScriptPath))
            {
                Log.Error("Python脚本路径为空");
                return new CommandResult { ExitCode = -1, Error = "Python script path cannot be empty" };
            }

            if (string.IsNullOrEmpty(venvPath))
            {
                Log.Error("虚拟环境路径为空");
                return new CommandResult { ExitCode = -1, Error = "Virtual environment path cannot be empty" };
            }

            if (!File.Exists(pythonScriptPath))
            {
                Log.Error("未找到Python脚本: {PythonScript}", pythonScriptPath);
                return new CommandResult { ExitCode = -1, Error = $"Python script not found: {pythonScriptPath}" };
            }

            var command = new StringBuilder();
            var activateScript = OperatingSystem.IsWindows()
                ? Path.Combine(venvPath, "Scripts", "activate.bat")
                : $"source {Path.Combine(venvPath, "bin", "activate")}";

            Log.Debug("使用虚拟环境激活脚本: {ActivateScript}", activateScript);

            command.Append(activateScript);
            command.Append(" && ");
            command.Append($"python {pythonScriptPath}");

            if (!string.IsNullOrEmpty(arguments))
            {
                command.Append($" {arguments}");
            }

            var fullCommand = command.ToString();
            Log.Debug("构建Python执行命令: {Command}", fullCommand);

            try
            {
                var result = await ExecuteCommandAsync(
                    fullCommand,
                    isShowTerminal,
                    onOutputReceived: onOutputReceived,
                    cancellationToken: cancellationToken);

                Log.Information("Python脚本执行完成: {PythonScript} 退出码: {ExitCode}",
                    pythonScriptPath, result.ExitCode);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "执行Python脚本失败: {PythonScript}", pythonScriptPath);
                return new CommandResult { ExitCode = -1, Error = ex.Message };
            }
        }

        /// <summary>
        /// 执行Python脚本并使用流式输出（用于实时解析JSON日志）
        /// </summary>
        /// <param name="pythonScriptPath">Python脚本路径</param>
        /// <param name="venvPath">虚拟环境路径</param>
        /// <param name="arguments">脚本参数</param>
        /// <param name="onStdoutLine">标准输出行处理回调（每行JSON）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>命令执行结果</returns>
        public static async Task<CommandResult> ExecutePythonScriptWithStreamingAsync(
            string pythonScriptPath,
            string venvPath,
            string? arguments = null,
            Action<string>? onStdoutLine = null,
            CancellationToken cancellationToken = default)
        {
            Log.Information("执行Python脚本(流式输出): {PythonScript}, 参数: {Arguments}",
                pythonScriptPath, arguments ?? "无");

            if (string.IsNullOrEmpty(pythonScriptPath))
            {
                Log.Error("Python脚本路径为空");
                return new CommandResult { ExitCode = -1, Error = "Python script path cannot be empty" };
            }

            if (string.IsNullOrEmpty(venvPath))
            {
                Log.Error("虚拟环境路径为空");
                return new CommandResult { ExitCode = -1, Error = "Virtual environment path cannot be empty" };
            }

            if (!File.Exists(pythonScriptPath))
            {
                Log.Error("未找到Python脚本: {PythonScript}", pythonScriptPath);
                return new CommandResult { ExitCode = -1, Error = $"Python script not found: {pythonScriptPath}" };
            }

            try
            {
                // 获取Python解释器路径
                var pythonExe = OperatingSystem.IsWindows()
                    ? Path.Combine(venvPath, "Scripts", "python.exe")
                    : Path.Combine(venvPath, "bin", "python");

                if (!File.Exists(pythonExe))
                {
                    Log.Error("未找到Python解释器: {PythonExe}", pythonExe);
                    return new CommandResult { ExitCode = -1, Error = $"Python executable not found: {pythonExe}" };
                }

                // 构建命令参数
                var scriptArgs = $"\"{pythonScriptPath}\"";
                if (!string.IsNullOrEmpty(arguments))
                {
                    scriptArgs += $" {arguments}";
                }

                Log.Debug("执行Python命令: {PythonExe} {ScriptArgs}", pythonExe, scriptArgs);

                var result = new CommandResult { ExitCode = 0 };
                var errorBuilder = new StringBuilder();
                int lineCount = 0;

                // 使用CliWrap的流式执行
                await foreach (var cmdEvent in Cli.Wrap(pythonExe)
                    .WithArguments(scriptArgs)
                    .WithValidation(CommandResultValidation.None)
                    .WithEnvironmentVariables(env =>
                    {
                        env.Set("PYTHONIOENCODING", "utf-8");
                        env.Set("PYTHONUNBUFFERED", "1");  // 禁用Python输出缓冲
                    })
                    .ListenAsync(cancellationToken))
                {
                    switch (cmdEvent)
                    {
                        case StandardOutputCommandEvent stdOut:
                            if (!string.IsNullOrWhiteSpace(stdOut.Text))
                            {
                                lineCount++;
                                Log.Debug("Python输出行 {LineCount}: {Text}", 
                                    lineCount, stdOut.Text.Substring(0, Math.Min(100, stdOut.Text.Length)));
                                
                                // 调用回调处理每一行输出
                                onStdoutLine?.Invoke(stdOut.Text);
                            }
                            break;

                        case StandardErrorCommandEvent stdErr:
                            if (!string.IsNullOrWhiteSpace(stdErr.Text))
                            {
                                Log.Warning("Python stderr: {Error}", stdErr.Text);
                                errorBuilder.AppendLine(stdErr.Text);
                            }
                            break;

                        case ExitedCommandEvent exited:
                            result.ExitCode = exited.ExitCode;
                            Log.Information("Python脚本执行完成，退出码: {ExitCode}, 总输出行数: {LineCount}",
                                exited.ExitCode, lineCount);
                            break;
                    }
                }

                result.Error = errorBuilder.ToString();
                return result;
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Python脚本执行被取消: {PythonScript}", pythonScriptPath);
                return new CommandResult { ExitCode = -999, Error = "Execution cancelled by user" };  // 使用特殊的退出码
            }
            catch (Exception ex)
            {
                Log.Error(ex, "执行Python脚本失败: {PythonScript}", pythonScriptPath);
                return new CommandResult { ExitCode = -1, Error = ex.Message };
            }
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

        /// <summary>
        /// 验证模型一致性
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="expectedModelName">期望的模型名称</param>
        /// <param name="venvPath">虚拟环境路径</param>
        /// <returns>验证结果(valid, model_name, message)</returns>
        public static async Task<(bool valid, string? modelName, string message)> ValidateModelConsistencyAsync(
            string modelPath, string expectedModelName, string venvPath)
        {
            Log.Information("验证模型一致性: {ModelPath}, 期望模型: {ExpectedModel}", 
                modelPath, expectedModelName);

            try
            {
                var validatorScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "PyScripts", "Utils", "ModelValidator.py");

                if (!File.Exists(validatorScript))
                {
                    Log.Error("模型验证脚本不存在: {Script}", validatorScript);
                    return (false, null, "错误：找不到模型验证工具");
                }

                var arguments = $"\"{modelPath}\" \"{expectedModelName}\"";
                var result = await ExecutePythonScriptAsync(validatorScript, venvPath, arguments);

                if (result.ExitCode != 0)
                {
                    Log.Error("模型验证脚本执行失败: {Error}", result.Error);
                    return (false, null, $"验证失败: {result.Error}");
                }

                // 解析JSON结果
                if (string.IsNullOrEmpty(result.Output))
                {
                    return (false, null, "验证脚本未返回结果");
                }

                var jsonResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result.Output);
                var valid = jsonResult.GetProperty("valid").GetBoolean();
                var modelName = jsonResult.TryGetProperty("model_name", out var nameElement) && nameElement.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? nameElement.GetString()
                    : null;
                var message = jsonResult.GetProperty("message").GetString() ?? "";

                Log.Information("模型验证结果: Valid={Valid}, ModelName={ModelName}, Message={Message}", 
                    valid, modelName, message);

                return (valid, modelName, message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "模型验证过程发生异常");
                return (false, null, $"验证异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从模型文件中读取模型名称（model_name metadata）
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="venvPath">Python虚拟环境路径</param>
        /// <returns>模型名称，如果读取失败返回null</returns>
        public static async Task<string?> GetModelNameFromMetadataAsync(string modelPath, string venvPath)
        {
            Log.Information("从模型文件读取model_name: {ModelPath}", modelPath);
            
            try
            {
                var validatorScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "PyScripts", "Utils", "ModelMetadataReader.py");
                
                if (!File.Exists(validatorScript))
                {
                    Log.Error("模型元数据读取脚本不存在: {Script}", validatorScript);
                    return null;
                }
                
                var arguments = $"\"{modelPath}\"";
                var result = await ExecutePythonScriptAsync(validatorScript, venvPath, arguments);
                
                if (result.ExitCode != 0)
                {
                    Log.Error("模型元数据读取失败: {Error}", result.Error);
                    return null;
                }
                
                // 解析JSON结果
                if (string.IsNullOrEmpty(result.Output))
                {
                    Log.Warning("元数据读取脚本未返回结果");
                    return null;
                }
                
                var jsonResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result.Output);
                var success = jsonResult.GetProperty("success").GetBoolean();
                
                if (!success)
                {
                    var message = jsonResult.GetProperty("message").GetString();
                    Log.Warning("读取模型元数据失败: {Message}", message);
                    return null;
                }
                
                var modelName = jsonResult.TryGetProperty("model_name", out var nameElement) && 
                               nameElement.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? nameElement.GetString()
                    : null;
                
                Log.Information("成功读取模型名称: {ModelName}", modelName);
                return modelName;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "读取模型元数据过程发生异常");
                return null;
            }
        }
    }
}