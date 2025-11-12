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
        public TrainModel? Config { get; set; }
        [JsonProperty("status")]
        public Status? Status { get; set; }
        [JsonProperty("entries")]
        public List<Entry>? Entries { get; set; }
    }

    public class TrainModel
    {
        // 任务类型
        [JsonProperty("task_type")]
        public string TaskType { get; set; } = "classification"; // "classification" or "detection"

        // 通用配置
        [JsonProperty("learning_rate")]
        public float LearningRate { get; set; }

        [JsonProperty("batch_size")]
        public int BatchSize { get; set; }

        [JsonProperty("epochs")]
        public int Epochs { get; set; }

        [JsonProperty("optimizer")]
        public string? Optimizer { get; set; }

        [JsonProperty("lr_scheduler")]
        public string? LrScheduler { get; set; }

        [JsonProperty("weight_decay")]
        public float WeightDecay { get; set; }

        [JsonProperty("early_stopping_rounds")]
        public int EarlyStoppingRounds { get; set; }

        [JsonProperty("early_stopping_delta")]
        public float EarlyStoppingDelta { get; set; }

        [JsonProperty("model_output_path")]
        public string? ModelOutputPath { get; set; }

        [JsonProperty("py_train_log_output_path")]
        public string? PyTrainLogOutputPath { get; set; }

        [JsonProperty("pretrained_model")]
        public string? PretrainedModel { get; set; }
        [JsonProperty("local_weights_path")]
        public string? LocalWeightsPath { get; set; }

        #region 分类任务专用
        [JsonProperty("train_data_path")]
        public string? ClassifyTrainImagesPath { get; set; }

        [JsonProperty("val_data_path")]
        public string? ClassifyValidImagesPath { get; set; }

        [JsonProperty("validation_split")]
        public float ClassifyValidImagesSplit { get; set; }
        #endregion

        #region 检测任务专用
        [JsonProperty("train_images_path")]
        public string? TrainImagesPath { get; set; } // 检测任务使用

        [JsonProperty("val_images_path")]
        public string? ValImagesPath { get; set; } // 检测任务使用

        [JsonProperty("train_annotation_path")]
        public string? TrainAnnotationPath { get; set; } // 检测任务使用

        [JsonProperty("val_annotation_path")]
        public string? ValAnnotationPath { get; set; } // 检测任务使用

        [JsonProperty("annotation_format")]
        public string? AnnotationFormat { get; set; } = "coco"; // "coco", "yolo", "pascal_voc"
        #endregion

        #region 通用参数
        [JsonProperty("num_classes")]
        public int NumClasses { get; set; } // 分类：类别数；检测：类别数+背景

        [JsonProperty("mutation_data_path")]
        public string? MutationDataPath { get; set; }
        #endregion

        #region 数据增强参数（分类任务使用）
        [JsonProperty("random_horizon_flip_checked")]
        public bool RandomHorizonFlipChecked { get; set; }
        [JsonProperty("random_vertical_flip_checked")]
        public bool RandomVerticalFlipChecked { get; set; }
        [JsonProperty("random_rotation_checked")]
        public bool RandomRotationChecked { get; set; }
        [JsonProperty("random_brightness_checked")]
        public bool RandomBrightnessChecked { get; set; }
        [JsonProperty("random_contrast_checked")]
        public bool RandomContrastChecked { get; set; }
        [JsonProperty("random_zoom_checked")]
        public bool RandomZoomChecked { get; set; }
        #endregion

        // 损失函数配置
        [JsonProperty("loss_function_config")]
        public LossFunctionModel LossFunction { get; set; } = new(); // 分类任务使用

        // 检测任务损失函数配置
        [JsonProperty("detection_loss_config")]
        public DetectionLossConfig DetectionLoss { get; set; } = new(); // 检测任务使用
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
        public string? Type { get; set; }
        [JsonProperty("message")]
        public string? Message { get; set; }
        [JsonProperty("epoch")]
        public int? Epoch { get; set; }
        [JsonProperty("metrics")]
        public Metrics? Metrics { get; set; }
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

    // 检测损失函数配置类
    public class DetectionLossConfig
    {
        [JsonProperty("rpn_cls_weight")]
        public float RpnClassificationWeight { get; set; } = 1.0f;

        [JsonProperty("rpn_bbox_weight")]
        public float RpnBoxRegressionWeight { get; set; } = 1.0f;

        [JsonProperty("roi_cls_weight")]
        public float RoIClassificationWeight { get; set; } = 1.0f;

        [JsonProperty("roi_bbox_weight")]
        public float RoIBoxRegressionWeight { get; set; } = 1.0f;

        [JsonProperty("focal_loss_alpha")]
        public float? FocalLossAlpha { get; set; } // RetinaNet专用

        [JsonProperty("focal_loss_gamma")]
        public float? FocalLossGamma { get; set; } = 2.0f; // RetinaNet专用

        [JsonProperty("iou_loss_type")]
        public string IouLossType { get; set; } = "iou"; // "iou", "giou", "diou", "ciou"

        [JsonProperty("mask_weight")]
        public float MaskWeight { get; set; } = 1.0f; // Mask R-CNN专用

        [JsonProperty("keypoint_weight")]
        public float KeypointWeight { get; set; } = 1.0f; // Keypoint R-CNN专用
    }
}