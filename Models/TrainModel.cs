using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class TrainModel
    {
        public string ModelName { get; set; }
        public int NumClasses { get; set; }
        public float LearningRate { get; set; }
        public string Optimizer { get; set; }
        public int BatchSize { get; set; }
        public int Epochs { get; set; }
        public bool UseDataAugmentation { get; set; }
        public string DataDir { get; set; }
        public string ModelSavePath { get; set; }
        public string StatusFile { get; set; }
    }
    public class TrainingStatus
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public int? Epoch { get; set; }
        public int? TotalEpochs { get; set; }
        public float? TrainLoss { get; set; }
        public float? TrainAcc { get; set; }
        public float? ValLoss { get; set; }
        public float? ValAcc { get; set; }
    }
}
