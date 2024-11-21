using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AutoTrainer.Models
{
    public class TrainingLog
    {
        public TrainModel Config { get; set; }
        public Status Status { get; set; }
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

        [JsonProperty("use_Data_augmentation")]
        public bool UseDataAugmentation { get; set; }
    }

    public class Status
    {
        public bool IsTraining { get; set; }
        public int CurrentEpoch { get; set; }
        public int TotalEpochs { get; set; }
        public double? BestValidationAccuracy { get; set; }
        public int EarlyStoppingCounter { get; set; }
        public double CurrentLearningRate { get; set; }
    }

    public class Entry
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public int? Epoch { get; set; }
        public Metrics Metrics { get; set; }
    }

    public class Metrics
    {
        public double TrainLoss { get; set; }
        public double TrainAccuracy { get; set; }
        public double ValidationLoss { get; set; }
        public double ValidationAccuracy { get; set; }
        public double LearningRate { get; set; }
    }

    public class EpochState
    {
        public int currentEpoch { get; set; }
        public int totallRpochs { get; set; }
    }
}