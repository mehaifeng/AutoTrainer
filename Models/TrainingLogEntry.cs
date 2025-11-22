using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 训练日志条目基类
    /// </summary>
    public class TrainingLogEntry
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 配置日志
    /// </summary>
    public class ConfigLogEntry : TrainingLogEntry
    {
        [JsonProperty("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonProperty("num_classes")]
        public int NumClasses { get; set; }
        
        [JsonProperty("batch_size")]
        public int BatchSize { get; set; }
        
        [JsonProperty("epochs")]
        public int Epochs { get; set; }
        
        [JsonProperty("learning_rate")]
        public double LearningRate { get; set; }
        
        [JsonProperty("device")]
        public string Device { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 进度日志
    /// </summary>
    public class ProgressLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("total_epochs")]
        public int TotalEpochs { get; set; }
        
        [JsonProperty("percent")]
        public double Percent { get; set; }
    }
    
    /// <summary>
    /// 指标日志
    /// </summary>
    public class MetricsLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("phase")]
        public string Phase { get; set; } = string.Empty;
        
        [JsonProperty("loss")]
        public double Loss { get; set; }
        
        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }
        
        [JsonProperty("lr")]
        public double LearningRate { get; set; }
    }
    
    /// <summary>
    /// 验证结果日志
    /// </summary>
    public class ValidationLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("metrics")]
        public Dictionary<string, double> Metrics { get; set; } = new();
        
        [JsonProperty("improved")]
        public bool Improved { get; set; }
    }
    
    /// <summary>
    /// 检查点日志
    /// </summary>
    public class CheckpointLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;
        
        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
        
        [JsonProperty("metrics")]
        public Dictionary<string, double>? Metrics { get; set; }
    }
    
    /// <summary>
    /// 学习率调整日志
    /// </summary>
    public class LRScheduleLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("old_lr")]
        public double OldLR { get; set; }
        
        [JsonProperty("new_lr")]
        public double NewLR { get; set; }
        
        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 早停日志
    /// </summary>
    public class EarlyStopLogEntry : TrainingLogEntry
    {
        [JsonProperty("epoch")]
        public int Epoch { get; set; }
        
        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
        
        [JsonProperty("best_epoch")]
        public int BestEpoch { get; set; }
        
        [JsonProperty("best_metrics")]
        public Dictionary<string, double> BestMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// 系统资源日志
    /// </summary>
    public class SystemLogEntry : TrainingLogEntry
    {
        [JsonProperty("gpu_usage")]
        public double? GPUUsage { get; set; }
        
        [JsonProperty("memory_usage")]
        public double? MemoryUsage { get; set; }
        
        [JsonProperty("cpu_usage")]
        public double? CPUUsage { get; set; }
    }
    
    /// <summary>
    /// 信息日志
    /// </summary>
    public class InfoLogEntry : TrainingLogEntry
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 警告日志
    /// </summary>
    public class WarningLogEntry : TrainingLogEntry
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 错误日志
    /// </summary>
    public class ErrorLogEntry : TrainingLogEntry
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        
        [JsonProperty("error_type")]
        public string? ErrorType { get; set; }
        
        [JsonProperty("traceback")]
        public string? Traceback { get; set; }
    }
    
    /// <summary>
    /// 完成日志
    /// </summary>
    public class CompleteLogEntry : TrainingLogEntry
    {
        [JsonProperty("best_epoch")]
        public int BestEpoch { get; set; }
        
        [JsonProperty("best_metrics")]
        public Dictionary<string, double> BestMetrics { get; set; } = new();
        
        [JsonProperty("model_path")]
        public string ModelPath { get; set; } = string.Empty;
        
        [JsonProperty("total_time")]
        public double TotalTime { get; set; }
    }
}
