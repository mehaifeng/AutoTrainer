using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    /// <summary>
    /// 包安装结果
    /// </summary>
    public class PackageInstallResult
    {
        public string PackageName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Version { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// 带多层Fallback机制的包安装器
    /// </summary>
    public static class PackageInstaller
    {
        /// <summary>
        /// 安装单个包（带多层Fallback）
        /// </summary>
        public static async Task<PackageInstallResult> InstallPackageWithFallbackAsync(
            string packageSpec,
            string pythonVenvPath,
            Action<string>? onOutput = null)
        {
            var result = new PackageInstallResult { PackageName = GetPackageName(packageSpec) };

            try
            {
                Log.Information("开始安装包: {Package}", packageSpec);

                // 策略1: 尝试直接安装指定版本
                onOutput?.Invoke($"尝试安装 {packageSpec}...");
                var installResult = await TryInstallPackageAsync(packageSpec, pythonVenvPath, onOutput);

                if (installResult.Success)
                {
                    Log.Information("✓ 成功安装 {Package}", packageSpec);
                    result.Success = true;
                    result.Version = installResult.Version;
                    return result;
                }

                result.Warnings.Add($"策略1失败: {installResult.ErrorMessage}");

                // 策略2: 尝试安装不带版本限制的包（使用pip的依赖解析）
                var packageName = GetPackageName(packageSpec);
                if (!string.IsNullOrEmpty(packageName) && packageName != packageSpec)
                {
                    onOutput?.Invoke($"策略1失败，尝试安装最新兼容版本 {packageName}...");
                    installResult = await TryInstallPackageAsync(packageName, pythonVenvPath, onOutput);

                    if (installResult.Success)
                    {
                        Log.Information("✓ 成功安装 {Package} (使用兼容版本)", packageName);
                        result.Success = true;
                        result.Version = installResult.Version;
                        result.Warnings.Add("使用了与要求版本不同的兼容版本");
                        return result;
                    }

                    result.Warnings.Add($"策略2失败: {installResult.ErrorMessage}");
                }

                // 策略3: 尝试使用--pre安装预发布版本（针对某些包）
                onOutput?.Invoke($"策略2失败，尝试安装预发布版本 {packageName}...");
                installResult = await TryInstallPackageAsync(
                    $"{packageName} --pre",
                    pythonVenvPath,
                    onOutput);

                if (installResult.Success)
                {
                    Log.Information("✓ 成功安装 {Package} (预发布版本)", packageName);
                    result.Success = true;
                    result.Version = installResult.Version;
                    result.Warnings.Add("使用了预发布版本");
                    return result;
                }

                result.Warnings.Add($"策略3失败: {installResult.ErrorMessage}");

                // 策略4: 尝试使用--no-deps安装（忽略依赖）
                onOutput?.Invoke($"策略3失败，尝试忽略依赖安装 {packageName}...");
                installResult = await TryInstallPackageAsync(
                    $"{packageName} --no-deps",
                    pythonVenvPath,
                    onOutput);

                if (installResult.Success)
                {
                    Log.Information("✓ 成功安装 {Package} (忽略依赖)", packageName);
                    result.Success = true;
                    result.Version = installResult.Version;
                    result.Warnings.Add("安装时忽略了依赖，可能需要手动安装依赖");
                    return result;
                }

                result.Warnings.Add($"策略4失败: {installResult.ErrorMessage}");

                // 所有策略都失败
                result.ErrorMessage = $"无法安装包 {packageSpec}，已尝试所有fallback策略";
                Log.Error("✗ 所有策略失败，无法安装 {Package}", packageSpec);
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Log.Error(ex, "安装包时发生异常: {Package}", packageSpec);
                return result;
            }
        }

        /// <summary>
        /// 尝试安装单个包
        /// </summary>
        private static async Task<(bool Success, string? Version, string? ErrorMessage)> TryInstallPackageAsync(
            string packageSpec,
            string pythonVenvPath,
            Action<string>? onOutput)
        {
            try
            {
                var activateFile = OperatingSystem.IsWindows()
                    ? $"{pythonVenvPath}\\Scripts\\activate.bat"
                    : $"source {pythonVenvPath}/bin/activate";

                var command = $"{activateFile} && pip3 install --quiet \"{packageSpec}\"";

                var result = await CliWrapHelper.ExecuteLine(
                    command,
                    isShowTerminal: false,
                    onOutputReceived: output => onOutput?.Invoke(output)
                );

                if (result.ExitCode == 0)
                {
                    // 尝试获取安装的版本
                    var version = await GetInstalledPackageVersionAsync(
                        GetPackageName(packageSpec),
                        pythonVenvPath);
                    return (true, version, null);
                }

                var errorMsg = result.Error ?? result.Output ?? "未知错误";
                return (false, null, errorMsg);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// 获取已安装包的版本
        /// </summary>
        private static async Task<string?> GetInstalledPackageVersionAsync(
            string packageName,
            string pythonVenvPath)
        {
            try
            {
                var activateFile = OperatingSystem.IsWindows()
                    ? $"{pythonVenvPath}\\Scripts\\activate.bat"
                    : $"source {pythonVenvPath}/bin/activate";

                var command = $"{activateFile} && pip show {packageName}";
                var result = await CliWrapHelper.ExecuteLine(command);

                // 如果第一次检查失败，尝试替换连字符为下划线
                if (result.ExitCode != 0 && packageName.Contains('-'))
                {
                    var altName = packageName.Replace('-', '_');
                    command = $"{activateFile} && pip show {altName}";
                    result = await CliWrapHelper.ExecuteLine(command);
                }

                if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.Output))
                {
                    // 解析版本号
                    var lines = result.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var versionLine = lines.FirstOrDefault(l => l.StartsWith("Version:", StringComparison.OrdinalIgnoreCase));
                    if (versionLine != null)
                    {
                        return versionLine.Split(':').Last().Trim();
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从包规格中提取包名
        /// </summary>
        private static string GetPackageName(string packageSpec)
        {
            // 处理各种包规格格式:
            // "package>=1.0.0" -> "package"
            // "package==1.0.0" -> "package"
            // "package --extra-url" -> "package"
            // "torch --index-url ..." -> "torch"

            var parts = packageSpec.Split(new[] { ' ', '=', '>', '<', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                return parts[0].Trim();
            }
            return packageSpec;
        }

        /// <summary>
        /// 批量安装包（优化顺序）
        /// </summary>
        public static async Task<List<PackageInstallResult>> InstallPackagesOptimizedAsync(
            List<string> packages,
            string pythonVenvPath,
            Action<string>? onOutput = null)
        {
            var results = new List<PackageInstallResult>();
            var packagesToInstall = new List<string>(packages);

            // 获取优化的安装顺序
            var preferredOrder = RequirementsManager.GetOptimizedInstallOrder();

            // 按优先级排序
            var sortedPackages = packagesToInstall
                .OrderByDescending(p => preferredOrder.IndexOf(GetPackageName(p)))
                .ThenBy(p => GetPackageName(p))
                .ToList();

            Log.Information("开始按优化顺序安装 {Count} 个包", sortedPackages.Count);

            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;

            foreach (var package in sortedPackages)
            {
                var packageName = GetPackageName(package);

                // 检查包是否已安装
                if (await IsPackageInstalledAsync(packageName, pythonVenvPath))
                {
                    onOutput?.Invoke($"⊘ {packageName} 已安装，跳过");
                    skipCount++;
                    results.Add(new PackageInstallResult
                    {
                        PackageName = packageName,
                        Success = true,
                        Warnings = new List<string> { "包已存在，未重新安装" }
                    });
                    continue;
                }

                // 安装包
                var result = await InstallPackageWithFallbackAsync(package, pythonVenvPath, onOutput);

                if (result.Success)
                {
                    successCount++;
                    onOutput?.Invoke($"✓ {packageName} 安装成功");
                }
                else
                {
                    failCount++;
                    onOutput?.Invoke($"✗ {packageName} 安装失败");

                    // 记录失败但继续
                    Log.Warning("包 {Package} 安装失败，继续安装其他包", packageName);
                }

                results.Add(result);
            }

            // 输出摘要
            var summary = new StringBuilder();
            summary.AppendLine("\n=== 安装摘要 ===");
            summary.AppendLine($"成功: {successCount}");
            summary.AppendLine($"跳过: {skipCount}");
            summary.AppendLine($"失败: {failCount}");

            if (failCount > 0)
            {
                summary.AppendLine("\n失败的包:");
                foreach (var fail in results.Where(r => !r.Success))
                {
                    summary.AppendLine($"  - {fail.PackageName}");
                }
            }

            onOutput?.Invoke(summary.ToString());
            Log.Information(summary.ToString());

            return results;
        }

        /// <summary>
        /// 检查包是否已安装
        /// </summary>
        private static async Task<bool> IsPackageInstalledAsync(string packageName, string pythonVenvPath)
        {
            try
            {
                var activateFile = OperatingSystem.IsWindows()
                    ? $"{pythonVenvPath}\\Scripts\\activate.bat"
                    : $"source {pythonVenvPath}/bin/activate";

                // 使用 pip show 检查包是否已安装
                // pip show 会自动处理连字符和下划线的等价性
                var command = $"{activateFile} && pip show {packageName}";
                var result = await CliWrapHelper.ExecuteLine(command);

                // 如果第一次检查失败，尝试替换连字符为下划线再检查
                if (result.ExitCode != 0 && packageName.Contains('-'))
                {
                    var altName = packageName.Replace('-', '_');
                    command = $"{activateFile} && pip show {altName}";
                    result = await CliWrapHelper.ExecuteLine(command);
                }

                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成安装报告
        /// </summary>
        public static string GenerateInstallReport(List<PackageInstallResult> results)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 包安装详细报告 ===");
            sb.AppendLine();

            var successful = results.Where(r => r.Success).ToList();
            var failed = results.Where(r => !r.Success).ToList();

            sb.AppendLine($"总计: {results.Count} | 成功: {successful.Count} | 失败: {failed.Count}");
            sb.AppendLine();

            if (successful.Count > 0)
            {
                sb.AppendLine("成功安装的包:");
                foreach (var result in successful)
                {
                    sb.Append($"  ✓ {result.PackageName}");
                    if (!string.IsNullOrEmpty(result.Version))
                        sb.Append($" ({result.Version})");

                    if (result.Warnings.Count > 0)
                        sb.Append($" [{string.Join("; ", result.Warnings)}]");

                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            if (failed.Count > 0)
            {
                sb.AppendLine("安装失败的包:");
                foreach (var result in failed)
                {
                    sb.AppendLine($"  ✗ {result.PackageName}");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                        sb.AppendLine($"    错误: {result.ErrorMessage}");

                    if (result.Warnings.Count > 0)
                    {
                        sb.AppendLine("    尝试的策略:");
                        foreach (var warning in result.Warnings)
                            sb.AppendLine($"      - {warning}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
