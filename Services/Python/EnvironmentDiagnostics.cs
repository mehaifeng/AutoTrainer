using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    /// <summary>
    /// 环境诊断报告
    /// </summary>
    public class DiagnosticReport
    {
        public string? PythonVersion { get; set; }
        public bool IsPythonCompatible { get; set; }
        public string? CUDAVersion { get; set; }
        public bool HasCUDA { get; set; }
        public bool HasGCC { get; set; }
        public bool HasMake { get; set; }
        public long DiskSpaceGB { get; set; }
        public string OSPlatform { get; set; } = string.Empty;
        public string OSArchitecture { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// 环境诊断和验证工具
    /// </summary>
    public static class EnvironmentDiagnostics
    {
        /// <summary>
        /// 检查命令是否可用
        /// </summary>
        private static async Task<bool> CheckCommand(string command)
        {
            try
            {
                var result = await CliWrapHelper.ExecuteLine(command);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取Python版本
        /// </summary>
        private static async Task<string?> GetPythonVersion(string pythonPath = "python")
        {
            try
            {
                var result = await CliWrapHelper.ExecuteLine($"{pythonPath} --version");
                if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.Output))
                {
                    // 输出格式: "Python 3.10.12"
                    var parts = result.Output.Split(' ');
                    if (parts.Length >= 2)
                    {
                        return parts[1].Trim();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取CUDA版本
        /// </summary>
        private static async Task<string?> GetCUDAVersion()
        {
            try
            {
                var result = await CliWrapHelper.ExecuteLine("nvidia-smi");
                if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.Output))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        result.Output,
                        @"CUDA Version:\s*(\d+\.\d+)"
                    );
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取可用磁盘空间
        /// </summary>
        private static long GetAvailableDiskSpace()
        {
            try
            {
                var drive = DriveInfo.GetDrives()[0]; // 主驱动器
                if (drive.IsReady)
                {
                    return drive.AvailableFreeSpace;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 检查Python版本兼容性
        /// </summary>
        private static bool CheckPythonCompatibility(string? version)
        {
            if (string.IsNullOrEmpty(version))
                return false;

            if (Version.TryParse(version, out var v))
            {
                // PyTorch支持Python 3.8-3.12
                return v.Major >= 3 && v.Minor >= 8 && v.Minor <= 12;
            }
            return false;
        }

        /// <summary>
        /// 执行完整的环境诊断
        /// </summary>
        public static async Task<DiagnosticReport> CheckEnvironment(string? pythonPath = null)
        {
            var report = new DiagnosticReport
            {
                OSPlatform = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString()
            };

            try
            {
                // 检查Python版本
                var pyPath = pythonPath ?? (OperatingSystem.IsWindows() ? "python" : "python3");
                report.PythonVersion = await GetPythonVersion(pyPath);
                report.IsPythonCompatible = CheckPythonCompatibility(report.PythonVersion);

                if (!report.IsPythonCompatible)
                {
                    report.Recommendations.Add(
                        $"Python {report.PythonVersion} 可能不兼容，建议使用 Python 3.9-3.11"
                    );
                }

                // 检查编译工具（Linux/macOS）
                if (!OperatingSystem.IsWindows())
                {
                    report.HasGCC = await CheckCommand("gcc --version");
                    report.HasMake = await CheckCommand("make --version");

                    if (!report.HasGCC && OperatingSystem.IsLinux())
                    {
                        var installCmd = RuntimeInformation.OSArchitecture == Architecture.Arm64
                            ? "sudo apt install build-essential"  // ARM64
                            : "sudo apt install build-essential";  // x64

                        report.Warnings.Add("未检测到gcc，部分包可能无法编译");
                        report.Recommendations.Add($"安装编译工具: {installCmd}");
                    }

                    if (!report.HasMake && OperatingSystem.IsLinux())
                    {
                        report.Recommendations.Add("安装make工具: sudo apt install make");
                    }
                }

                // 检查CUDA
                report.CUDAVersion = await GetCUDAVersion();
                report.HasCUDA = !string.IsNullOrEmpty(report.CUDAVersion);

                if (report.HasCUDA && !string.IsNullOrEmpty(report.CUDAVersion))
                {
                    Log.Information("检测到CUDA版本: {CUDAVersion}", report.CUDAVersion);

                    // 检查CUDA版本是否太低
                    if (double.TryParse(report.CUDAVersion, out double cudaVer))
                    {
                        if (cudaVer < 12.1)
                        {
                            report.Warnings.Add($"CUDA {report.CUDAVersion} 版本较低，PyTorch将使用CPU版本");
                        }
                    }
                }
                else
                {
                    Log.Information("未检测到CUDA，将使用CPU版本");
                }

                // 检查磁盘空间
                report.DiskSpaceGB = GetAvailableDiskSpace() / (1024 * 1024 * 1024);

                if (report.DiskSpaceGB < 5)
                {
                    report.Warnings.Add($"磁盘空间不足5GB (当前可用: {report.DiskSpaceGB}GB)");
                    report.Recommendations.Add("建议清理磁盘空间，至少预留5GB用于安装深度学习环境");
                }
                else if (report.DiskSpaceGB < 10)
                {
                    report.Warnings.Add($"磁盘空间较少 (当前可用: {report.DiskSpaceGB}GB)");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "环境诊断时发生错误");
                report.Warnings.Add($"环境诊断出错: {ex.Message}");
            }

            return report;
        }

        /// <summary>
        /// 格式化诊断报告为可读文本
        /// </summary>
        public static string FormatReport(DiagnosticReport report)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 环境诊断报告 ===");
            sb.AppendLine();

            sb.AppendLine("系统信息:");
            sb.AppendLine($"  操作系统: {report.OSPlatform}");
            sb.AppendLine($"  架构: {report.OSArchitecture}");
            sb.AppendLine($"  可用磁盘空间: {report.DiskSpaceGB} GB");
            sb.AppendLine();

            sb.AppendLine("Python环境:");
            sb.AppendLine($"  Python版本: {report.PythonVersion ?? "未检测到"}");
            sb.AppendLine($"  兼容性: {(report.IsPythonCompatible ? "✓ 兼容" : "✗ 不兼容")}");
            sb.AppendLine();

            sb.AppendLine("编译工具:");
            sb.AppendLine($"  GCC: {(report.HasGCC ? "✓ 已安装" : "✗ 未安装")}");
            sb.AppendLine($"  Make: {(report.HasMake ? "✓ 已安装" : "✗ 未安装")}");
            sb.AppendLine();

            sb.AppendLine("GPU支持:");
            sb.AppendLine($"  CUDA: {(report.HasCUDA ? $"✓ {report.CUDAVersion}" : "✗ 未安装")}");
            sb.AppendLine();

            if (report.Warnings.Count > 0)
            {
                sb.AppendLine("警告:");
                foreach (var warning in report.Warnings)
                {
                    sb.AppendLine($"  ⚠ {warning}");
                }
                sb.AppendLine();
            }

            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("建议:");
                foreach (var rec in report.Recommendations)
                {
                    sb.AppendLine($"  → {rec}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 根据环境获取推荐的PyTorch安装命令
        /// </summary>
        public static string GetRecommendedPyTorchInstallCommand(DiagnosticReport report)
        {
            if (!report.HasCUDA || string.IsNullOrEmpty(report.CUDAVersion))
            {
                // CPU版本
                return "pip install torch torchvision";
            }

            if (double.TryParse(report.CUDAVersion, out double cudaVer))
            {
                // PyTorch官方支持的CUDA版本
                if (cudaVer >= 13.0)
                {
                    return "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu130";
                }
                else if (cudaVer >= 12.8)
                {
                    return "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128";
                }
                else if (cudaVer >= 12.1)
                {
                    return "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126";
                }
                else if (cudaVer >= 12.0)
                {
                    return "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121";
                }
                else if (cudaVer >= 11.8)
                {
                    return "pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118";
                }
            }

            // CUDA版本过低或无法识别，使用CPU版本
            return "pip install torch torchvision";
        }

        /// <summary>
        /// 检测是否应该使用预编译wheel
        /// </summary>
        public static bool ShouldUsePrecompiledWheel(DiagnosticReport report)
        {
            // 在以下情况下优先使用预编译wheel:
            // 1. ARM64架构（编译困难）
            if (report.OSArchitecture.Contains("arm") || report.OSArchitecture.Contains("aarch64"))
            {
                return true;
            }

            // 2. 没有编译工具的Linux系统
            if (OperatingSystem.IsLinux() && !report.HasGCC)
            {
                return true;
            }

            // 3. Windows系统（通常使用预编译版本）
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            return false;
        }
    }
}
