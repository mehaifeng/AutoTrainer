using AutoTrainer.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class TrainingLog
    {
        [JsonProperty("config")]
        public TrainModel Config { get; set; }
        [JsonProperty("status")]
        public Status Status { get; set; }
        [JsonProperty("entries")]
        public List<Entry> Entries { get; set; }
    }

    public class TrainModel
    {
        [JsonProperty("learning_rate")]
        public float LearningRate { get; set; }

        [JsonProperty("batch_size")]
        public int BatchSize { get; set; }

        [JsonProperty("epochs")]
        public int Epochs { get; set; }

        [JsonProperty("optimizer")]
        public string Optimizer { get; set; }

        [JsonProperty("validation_split")]
        public float ValidationSplit { get; set; }

        [JsonProperty("lr_scheduler")]
        public string LrScheduler { get; set; }

        [JsonProperty("weight_decay")]
        public float WeightDecay { get; set; }

        [JsonProperty("early_stopping_rounds")]
        public int EarlyStoppingRounds { get; set; }

        [JsonProperty("model_output_path")]
        public string ModelOutputPath { get; set; }

        [JsonProperty("log_output_path")]
        public string LogOutputPath { get; set; }

        [JsonProperty("pretrained_model")]
        public string PretrainedModel { get; set; }

        [JsonProperty("train_data_path")]
        public string TrainDataPath { get; set; }

        [JsonProperty("val_data_path")]
        public string ValDataPath { get; set; }
        [JsonProperty("mutation_data_path")]
        public string MutationDataPath { get; set; }

        [JsonProperty("use_Data_augmentation")]
        public bool UseDataAugmentation { get; set; }
    }

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

    public class Entry
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("epoch")]
        public int? Epoch { get; set; }
        [JsonProperty("metrics")]
        public Metrics Metrics { get; set; }
    }

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

    public partial class EpochState : ViewModelBase
    {
        [ObservableProperty] private int? currentEpoch;
        [ObservableProperty] private int? totalEpochs;
    }
}