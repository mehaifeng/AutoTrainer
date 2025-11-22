using AutoTrainer.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 训练日志容器
    /// </summary>
    public class TrainingLog
    {
        [JsonProperty("config")]
        public TrainModel? Config { get; set; }
        
        [JsonProperty("status")]
        public Status? Status { get; set; }
        
        [JsonProperty("entries")]
        public List<Entry>? Entries { get; set; }
    }

    /// <summary>
    /// 训练状态
    /// </summary>
    public class Status
    {
        [JsonProperty("is_training")]
        public bool IsTraining { get; set; }
        
        [JsonProperty("current_epoch")]
        public int CurrentEpoch { get; set; }
        
        [JsonProperty("total_epochs")]
        public int TotalEpochs { get; set; }
        
        [JsonProperty("best_validation_accuracy")]
        public float? BestValidationAccuracy { get; set; }
        
        [JsonProperty("early_stopping_counter")]
        public int EarlyStoppingCounter { get; set; }
        
        [JsonProperty("current_learning_rate")]
        public float? CurrentLearningRate { get; set; }
    }

    /// <summary>
    /// 训练日志条目
    /// </summary>
    public class Entry
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonProperty("type")]
        public string? Type { get; set; }
        
        [JsonProperty("message")]
        public string? Message { get; set; }
        
        [JsonProperty("epoch")]
        public int? Epoch { get; set; }
        
        [JsonProperty("metrics")]
        public Metrics? Metrics { get; set; }
    }

    /// <summary>
    /// 训练指标
    /// </summary>
    public class Metrics
    {
        [JsonProperty("train_loss")]
        public float? TrainLoss { get; set; }
        
        [JsonProperty("train_accuracy")]
        public float? TrainAccuracy { get; set; }
        
        [JsonProperty("validation_loss")]
        public float? ValidationLoss { get; set; }
        
        [JsonProperty("validation_accuracy")]
        public float? ValidationAccuracy { get; set; }
        
        [JsonProperty("learning_rate")]
        public float? LearningRate { get; set; }
    }

    /// <summary>
    /// 训练周期状态（UI绑定用）
    /// </summary>
    public partial class EpochState : ViewModelBase
    {
        [ObservableProperty] 
        private int? currentEpoch = 0;
        [ObservableProperty] 
        private int? totalEpochs = 100;
    }
}
