using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoTrainer.CmdHelper
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
        /// 执行一条指令
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
        /// 执行多条指令
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
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // 启动进程
            return await Task.Factory.StartNew(() =>
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    // 激活虚拟环境
                    using (var writer = process.StandardInput)
                    {
                        if (writer.BaseStream.CanWrite)
                        {
                            foreach (var command in commands)
                            {
                                writer.WriteLine(command);
                            }
                        }
                    }
                    // 读取输出
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return (process.ExitCode, output);
                }
            });
        }
    }
}
