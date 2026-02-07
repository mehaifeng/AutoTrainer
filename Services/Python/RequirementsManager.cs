using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    /// <summary>
    /// 灵活的需求包配置
    /// 支持版本范围、平台特定配置等
    /// </summary>
    public class FlexibleRequirements
    {
        [JsonProperty("base")]
        public BasePackages? Base { get; set; }

        [JsonProperty("core")]
        public CorePackages? Core { get; set; }

        [JsonProperty("ml")]
        public MLPackages? Ml { get; set; }

        [JsonProperty("optional")]
        public Dictionary<string, List<string>>? Optional { get; set; }
    }

    public class BasePackages
    {
        [JsonProperty("pip")]
        public List<string>? Pip { get; set; }
    }

    public class CorePackages
    {
        [JsonProperty("all")]
        public List<string>? All { get; set; }

        [JsonProperty("windows")]
        public List<string>? Windows { get; set; }

        [JsonProperty("linux")]
        public List<string>? Linux { get; set; }

        [JsonProperty("darwin")]
        public List<string>? Darwin { get; set; }
    }

    public class MLPackages
    {
        [JsonProperty("cpu")]
        public CPUPackages? Cpu { get; set; }

        [JsonProperty("cuda")]
        public Dictionary<string, CPUPackages>? Cuda { get; set; }
    }

    public class CPUPackages
    {
        [JsonProperty("torch")]
        public string? Torch { get; set; }

        [JsonProperty("torchvision")]
        public string? Torchvision { get; set; }

        [JsonProperty("tensorflow")]
        public string? TensorFlow { get; set; }
    }

    /// <summary>
    /// 灵活的需求包管理器
    /// </summary>
    public static class RequirementsManager
    {
        private static string FlexibleRequirementsFilePath =>
            Path.Combine(Environment.CurrentDirectory, "Configs", "FlexibleRequirements.json");

        /// <summary>
        /// 获取优化的安装顺序
        /// 基础依赖 -> 核心库 -> ML框架 -> 其他工具
        /// </summary>
        public static List<string> GetOptimizedInstallOrder()
        {
            return new List<string>
            {
                // 第一优先级: pip工具本身
                "setuptools",
                "wheel",
                "pip",

                // 第二优先级: 基础科学计算（很多包依赖numpy）
                "numpy",
                "scipy",

                // 第三优先级: 图像和数据处理
                "pillow",
                "opencv-python",

                // 第四优先级: 可视化和数据分析
                "matplotlib",
                "pandas",
                "scikit-learn",

                // 第五优先级: 深度学习框架（依赖最多，最后安装）
                "torch",  // 应该通过特殊方法安装
                "torchvision",
                "tensorflow",

                // 第六优先级: 其他工具
                "tqdm",
                "albumentations",
                "onnx",
                "tf_keras",
                "psutil"
            };
        }

        /// <summary>
        /// 从灵活配置加载需求包
        /// </summary>
        public static async Task<List<string>> LoadRequirementsAsync(
            bool useCUDA = false,
            string? cudaVersion = null)
        {
            var packages = new List<string>();

            try
            {
                if (!File.Exists(FlexibleRequirementsFilePath))
                {
                    Log.Warning("灵活配置文件不存在: {Path}", FlexibleRequirementsFilePath);
                    return await GetDefaultRequirementsAsync(useCUDA, cudaVersion);
                }

                var jsonContent = await File.ReadAllTextAsync(FlexibleRequirementsFilePath);
                var config = JsonConvert.DeserializeObject<FlexibleRequirements>(jsonContent);

                if (config == null)
                {
                    return await GetDefaultRequirementsAsync(useCUDA, cudaVersion);
                }

                // 1. 添加基础pip工具
                if (config.Base?.Pip != null)
                {
                    packages.AddRange(config.Base.Pip);
                }

                // 2. 添加核心包（通用）
                if (config.Core?.All != null)
                {
                    packages.AddRange(config.Core.All);
                }

                // 3. 添加平台特定包
                if (config.Core != null)
                {
                    if (OperatingSystem.IsWindows() && config.Core.Windows != null)
                    {
                        packages.AddRange(config.Core.Windows);
                    }
                    else if (OperatingSystem.IsLinux() && config.Core.Linux != null)
                    {
                        packages.AddRange(config.Core.Linux);
                    }
                    else if (OperatingSystem.IsMacOS() && config.Core.Darwin != null)
                    {
                        packages.AddRange(config.Core.Darwin);
                    }
                }

                // 4. 添加ML框架
                if (config.Ml != null)
                {
                    var mlPackages = useCUDA && !string.IsNullOrEmpty(cudaVersion)
                        ? GetCudaPackages(config.Ml, cudaVersion)
                        : config.Ml.Cpu;

                    if (mlPackages != null)
                    {
                        if (!string.IsNullOrEmpty(mlPackages.Torch))
                            packages.Add(mlPackages.Torch);
                        if (!string.IsNullOrEmpty(mlPackages.Torchvision))
                            packages.Add(mlPackages.Torchvision);
                    }
                }

                Log.Information("从灵活配置加载了 {Count} 个包", packages.Count);
                return packages;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载灵活配置失败，使用默认配置");
                return await GetDefaultRequirementsAsync(useCUDA, cudaVersion);
            }
        }

        /// <summary>
        /// 获取CUDA版本的包配置
        /// </summary>
        private static CPUPackages? GetCudaPackages(MLPackages mlConfig, string cudaVersion)
        {
            if (mlConfig.Cuda == null)
                return mlConfig.Cpu;

            // 解析CUDA版本并匹配最佳配置
            if (double.TryParse(cudaVersion, out double cudaVer))
            {
                // 从高到低匹配版本
                if (cudaVer >= 13.0 && mlConfig.Cuda.ContainsKey("cu130"))
                    return mlConfig.Cuda["cu130"];
                if (cudaVer >= 12.8 && mlConfig.Cuda.ContainsKey("cu128"))
                    return mlConfig.Cuda["cu128"];
                if (cudaVer >= 12.6 && mlConfig.Cuda.ContainsKey("cu126"))
                    return mlConfig.Cuda["cu126"];
                if (cudaVer >= 12.4 && mlConfig.Cuda.ContainsKey("cu124"))
                    return mlConfig.Cuda["cu124"];
                if (cudaVer >= 12.1 && mlConfig.Cuda.ContainsKey("cu121"))
                    return mlConfig.Cuda["cu121"];
                if (cudaVer >= 11.8 && mlConfig.Cuda.ContainsKey("cu118"))
                    return mlConfig.Cuda["cu118"];
            }

            // 找不到匹配的CUDA版本，使用CPU版本
            Log.Warning("未找到CUDA {Version}的配置，使用CPU版本", cudaVersion);
            return mlConfig.Cpu;
        }

        /// <summary>
        /// 获取默认需求包（向后兼容）
        /// </summary>
        private static async Task<List<string>> GetDefaultRequirementsAsync(
            bool useCUDA,
            string? cudaVersion)
        {
            return await Task.Run(() =>
            {
                var packages = new List<string>
                {
                    // 基础工具
                    "setuptools>=65.0.0",
                    "wheel>=0.40.0",

                    // 核心科学计算
                    "numpy>=1.24.0,<2.0.0",
                    "scipy>=1.10.0",

                    // 图像处理
                    "pillow>=9.0.0",
                    "opencv-python>=4.8.0",

                    // 可视化
                    "matplotlib>=3.7.0",

                    // 机器学习
                    "scikit-learn>=1.3.0",

                    // 数据处理
                    "pandas>=2.0.0",

                    // 工具
                    "tqdm>=4.65.0",
                    "albumentations>=1.3.0",

                    // 模型转换
                    "onnx>=1.14.0",
                    "tensorflow>=2.13.0",

                    // 其他
                    "tf_keras",
                    "psutil>=5.9.0",
                    "sympy>=1.12",
                    "six>=1.16.0"
                };

                // 根据CUDA添加PyTorch
                if (useCUDA && !string.IsNullOrEmpty(cudaVersion))
                {
                    var torchCommand = GetPyTorchCudaCommand(cudaVersion);
                    packages.Add(torchCommand);
                }
                else
                {
                    packages.Add("torch>=2.0.0,<3.0.0");
                    packages.Add("torchvision>=0.15.0,<1.0.0");
                }

                return packages;
            });
        }

        /// <summary>
        /// 获取PyTorch CUDA安装命令
        /// </summary>
        private static string GetPyTorchCudaCommand(string cudaVersion)
        {
            if (double.TryParse(cudaVersion, out double cudaVer))
            {
                if (cudaVer >= 13.0)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu130";
                if (cudaVer >= 12.8)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu128";
                if (cudaVer >= 12.6)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu126";
                if (cudaVer >= 12.4)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu124";
                if (cudaVer >= 12.1)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu121";
                if (cudaVer >= 11.8)
                    return "torch torchvision --index-url https://download.pytorch.org/whl/cu118";
            }

            // CUDA版本过低或不匹配，使用CPU版本
            Log.Warning("CUDA {Version}不受支持，使用CPU版本PyTorch", cudaVersion);
            return "torch>=2.0.0 torchvision>=0.15.0";
        }

        /// <summary>
        /// 创建示例灵活配置文件
        /// </summary>
        public static async Task CreateSampleConfigAsync()
        {
            var sampleConfig = new FlexibleRequirements
            {
                Base = new BasePackages
                {
                    Pip = new List<string>
                    {
                        "setuptools>=65.0.0",
                        "wheel>=0.40.0",
                        "pip>=23.0.0"
                    }
                },
                Core = new CorePackages
                {
                    All = new List<string>
                    {
                        "numpy>=1.24.0,<2.0.0",
                        "scipy>=1.10.0",
                        "pillow>=9.0.0",
                        "matplotlib>=3.7.0",
                        "scikit-learn>=1.3.0",
                        "pandas>=2.0.0",
                        "tqdm>=4.65.0",
                        "albumentations>=1.3.0",
                        "onnx>=1.14.0",
                        "psutil>=5.9.0",
                        "sympy>=1.12",
                        "six>=1.16.0"
                    },
                    Windows = new List<string>
                    {
                        "opencv-python>=4.8.0"
                    },
                    Linux = new List<string>
                    {
                        "opencv-python>=4.8.0"
                    },
                    Darwin = new List<string>
                    {
                        "opencv-python>=4.8.0"
                    }
                },
                Ml = new MLPackages
                {
                    Cpu = new CPUPackages
                    {
                        Torch = "torch>=2.0.0,<3.0.0",
                        Torchvision = "torchvision>=0.15.0,<1.0.0",
                        TensorFlow = "tensorflow>=2.13.0"
                    },
                    Cuda = new Dictionary<string, CPUPackages>
                    {
                        ["cu118"] = new CPUPackages
                        {
                            Torch = "torch --index-url https://download.pytorch.org/whl/cu118",
                            Torchvision = "torchvision --index-url https://download.pytorch.org/whl/cu118"
                        },
                        ["cu121"] = new CPUPackages
                        {
                            Torch = "torch --index-url https://download.pytorch.org/whl/cu121",
                            Torchvision = "torchvision --index-url https://download.pytorch.org/whl/cu121"
                        },
                        ["cu126"] = new CPUPackages
                        {
                            Torch = "torch --index-url https://download.pytorch.org/whl/cu126",
                            Torchvision = "torchvision --index-url https://download.pytorch.org/whl/cu126"
                        },
                        ["cu128"] = new CPUPackages
                        {
                            Torch = "torch --index-url https://download.pytorch.org/whl/cu128",
                            Torchvision = "torchvision --index-url https://download.pytorch.org/whl/cu128"
                        },
                        ["cu130"] = new CPUPackages
                        {
                            Torch = "torch --index-url https://download.pytorch.org/whl/cu130",
                            Torchvision = "torchvision --index-url https://download.pytorch.org/whl/cu130"
                        }
                    }
                },
                Optional = new Dictionary<string, List<string>>
                {
                    ["development"] = new List<string>
                    {
                        "jupyter>=1.0.0",
                        "ipython>=8.0.0",
                        "ipykernel>=6.0.0"
                    },
                    ["visualization"] = new List<string>
                    {
                        "seaborn>=0.12.0",
                        "plotly>=5.0.0"
                    }
                }
            };

            var json = JsonConvert.SerializeObject(sampleConfig, Formatting.Indented);
            await File.WriteAllTextAsync(FlexibleRequirementsFilePath, json);
            Log.Information("创建示例配置文件: {Path}", FlexibleRequirementsFilePath);
        }
    }
}
