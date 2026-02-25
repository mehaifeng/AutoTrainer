using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public class SystemPerformanceInfoHelper
    {
        private readonly CancellationTokenSource _refreshCts = new();
        private readonly PerformanceCounter? _cpuCounter;
        private ulong? _totalPhysicalMemory; // 缓存总内存大小

        public SystemPerformanceInfoHelper()
        {
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // 第一次调用需要预热
                _cpuCounter.NextValue();

                // 获取总内存（只需查询一次）
                _totalPhysicalMemory = GetTotalPhysicalMemoryWindows();
            }
        }
        /// <summary>
        /// 获取系统硬件信息
        /// </summary>
        /// <returns>元组(cpu,gpu,ram)</returns>
        public async Task<(string,string,string)> GetSystemInfoAsync()
        {
            try
            {
                // 顺序获取，避免并发导致的采样问题
                var cpu = await GetCpuRateAsync();
                var ram = await GetRamRateAsync();
                var gpu = await GetGpuRateAsync();

                var cpuRate = cpu >= 0 ? $"{cpu}%" : "N/A";
                var gpuRate = gpu >= 0 ? $"{gpu}%" : "N/A";
                var ramRate = ram >= 0 ? $"{ram}%" : "N/A";
                
                return (cpuRate, gpuRate, ramRate);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "获取系统信息时出现异常");
                return ("N/A", "N/A", "N/A");
            }
        }
        /// <summary>
        /// 获取 CPU 占用率
        /// </summary>
        private async Task<int> GetCpuRateAsync()
        {
            if (OperatingSystem.IsWindows() && _cpuCounter != null)
            {
                // Windows使用PerformanceCounter
#pragma warning disable CA1416 // 验证平台兼容性
                return await Task.Run(() => (int)Math.Round(_cpuCounter.NextValue()));
#pragma warning restore CA1416 // 验证平台兼容性
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux: 读取 /proc/stat
                return await GetCpuRateLinuxAsync();
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS: 使用 host_processor_info API 或降级到命令行
                return await GetCpuRateMacOSAsync();
            }

            return -1;
        }

        /// <summary>
        /// Windows 获取总物理内存（使用 P/Invoke）
        /// </summary>
        private ulong GetTotalPhysicalMemoryWindows()
        {
            if (OperatingSystem.IsWindows())
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return memStatus.ullTotalPhys;
                }
            }
            return 0;
        }

        /// <summary>
        /// Windows RAM 占用率
        /// </summary>
        private async Task<int> GetRamRateWindowsAsync()
        {
            return await Task.Run(() =>
            {
                if (_totalPhysicalMemory == null || _totalPhysicalMemory == 0)
                    return -1;

                var memStatus = new MEMORYSTATUSEX();
                if (!GlobalMemoryStatusEx(memStatus))
                    return -1;

                var usedMemory = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
                var usedPercent = 100.0 * usedMemory / memStatus.ullTotalPhys;
                return (int)Math.Round(usedPercent);
            });
        }

        // Windows API P/Invoke
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        /// <summary>
        /// Linux CPU 占用率（读取 /proc/stat）
        /// </summary>
        private async Task<int> GetCpuRateLinuxAsync()
        {
            try
            {
                // 第一次采样
                var (idle1, total1) = await ReadProcStatAsync();
                await Task.Delay(1000); // 采样间隔
                var (idle2, total2) = await ReadProcStatAsync();

                var idleDiff = idle2 - idle1;
                var totalDiff = total2 - total1;

                if (totalDiff == 0) return 0;

                var usage = 100.0 * (1.0 - (double)idleDiff / totalDiff);
                return (int)Math.Round(Math.Max(0, Math.Min(100, usage)));
            }
            catch
            {
                return -1;
            }
        }

        private async Task<(long idle, long total)> ReadProcStatAsync()
        {
            var lines = await File.ReadAllLinesAsync("/proc/stat");
            var cpuLine = lines[0]; // "cpu  user nice system idle iowait irq softirq..."
            var values = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            long idle = long.Parse(values[4]);
            long total = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (long.TryParse(values[i], out var v))
                    total += v;
            }

            return (idle, total);
        }

        /// <summary>
        /// macOS CPU 占用率
        /// </summary>
        private async Task<int> GetCpuRateMacOSAsync()
        {
            var cmd = @"ps -A -o %cpu | awk '{s+=$1} END {print s}'";
            var result = await CliWrapHelper.ExecuteLine(cmd, enableVerboseLogging: false);

            if (result.ExitCode == 0 && float.TryParse(result.Output?.Trim(), out var percent))
            {
                return (int)Math.Round(Math.Min(100, percent));
            }
            return -1;
        }

        /// <summary>
        /// 获取 RAM 占用率
        /// </summary>
        private async Task<int> GetRamRateAsync()
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: 使用 WMI 或 PerformanceCounter
                return await GetRamRateWindowsAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux: 读取 /proc/meminfo（比 free 高效）
                return await GetRamRateLinuxAsync();
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS: 使用 vm_stat
                var cmd = @"vm_stat | awk '/Pages free/ {free=$3} /Pages active/ {active=$3} /Pages inactive/ {inactive=$3} /Pages speculative/ {spec=$3} /Pages wired/ {wired=$3} END {used=active+wired; total=free+active+inactive+spec+wired; print int(100*used/total)}'";
                var result = await CliWrapHelper.ExecuteLine(cmd, enableVerboseLogging: false);

                if (result.ExitCode == 0 && int.TryParse(result.Output?.Trim(), out var percent))
                {
                    return percent;
                }
            }

            return -1;
        }

        private async Task<int> GetRamRateLinuxAsync()
        {
            try
            {
                var lines = await File.ReadAllLinesAsync("/proc/meminfo");
                long total = 0, available = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        total = ParseMemInfoValue(line);
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        available = ParseMemInfoValue(line);
                        break;
                    }
                }

                if (total > 0)
                {
                    var usedPercent = 100.0 * (1.0 - (double)available / total);
                    return (int)Math.Round(usedPercent);
                }
            }
            catch { }

            return -1;
        }

        private long ParseMemInfoValue(string line)
        {
            // "MemTotal:       16384000 kB"
            var parts = line.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var valueStr = parts[1].Split(' ')[0];
                if (long.TryParse(valueStr, out var value))
                    return value;
            }
            return 0;
        }

        /// <summary>
        /// 获取 GPU 占用率（仅 NVIDIA）
        /// </summary>
        private async Task<int> GetGpuRateAsync()
        {
            // 缓存检测结果，避免每次都检查
            if (!_hasNvidiaSmi.HasValue)
            {
                string checkCmd = OperatingSystem.IsWindows() ? "where nvidia-smi" : "which nvidia-smi";
                var check = await CliWrapHelper.ExecuteLine(checkCmd, enableVerboseLogging: false);
                _hasNvidiaSmi = check.ExitCode == 0;
            }

            if (_hasNvidiaSmi == false) return -1;

            var cmd = @"nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits";
            var result = await CliWrapHelper.ExecuteLine(cmd, enableVerboseLogging: false);

            if (result.ExitCode == 0 && int.TryParse(result.Output?.Trim(), out var percent))
            {
                return percent;
            }

            return -1;
        }

        private bool? _hasNvidiaSmi;

        public void Dispose()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _cpuCounter?.Dispose();
        }
    }
}
