using Newtonsoft.Json;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 训练任务基础配置（通用参数）
    /// </summary>
    public class TrainConfigBase
    {
        [JsonProperty("task_type")]
        public string TaskType { get; set; } = "classification"; // "classification" or "detection"

        // 优化器配置
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

        // 早停配置
        [JsonProperty("early_stopping_rounds")]
        public int EarlyStoppingRounds { get; set; }

        [JsonProperty("early_stopping_delta")]
        public float EarlyStoppingDelta { get; set; }

        // 输出路径
        [JsonProperty("model_output_path")]
        public string? ModelOutputPath { get; set; }

        [JsonProperty("py_train_log_output_path")]
        public string? PyTrainLogOutputPath { get; set; }

        // 预训练模型
        [JsonProperty("pretrained_model")]
        public string? PretrainedModel { get; set; }

        [JsonProperty("local_weights_path")]
        public string? LocalWeightsPath { get; set; }

        [JsonProperty("num_classes")]
        public int NumClasses { get; set; }
    }

    /// <summary>
    /// 分类任务配置
    /// </summary>
    public class ClassificationConfig
    {
        [JsonProperty("train_data_path")]
        public string? TrainDataPath { get; set; }

        [JsonProperty("val_data_path")]
        public string? ValDataPath { get; set; }

        [JsonProperty("validation_split")]
        public float ValidationSplit { get; set; }

        [JsonProperty("mutation_data_path")]
        public string? MutationDataPath { get; set; }

        [JsonProperty("data_augmentation")]
        public DataAugmentationConfig DataAugmentation { get; set; } = new();

        [JsonProperty("loss_function_config")]
        public LossFunctionModel LossFunction { get; set; } = new();
    }

    /// <summary>
    /// 目标检测任务配置
    /// </summary>
    public class DetectionConfig
    {
        [JsonProperty("train_images_path")]
        public string? TrainImagesPath { get; set; }

        [JsonProperty("val_images_path")]
        public string? ValImagesPath { get; set; }

        [JsonProperty("train_annotation_path")]
        public string? TrainAnnotationPath { get; set; }

        [JsonProperty("val_annotation_path")]
        public string? ValAnnotationPath { get; set; }

        [JsonProperty("annotation_format")]
        public string AnnotationFormat { get; set; } = "coco"; // "coco", "yolo", "pascal_voc"

        [JsonProperty("detection_loss_config")]
        public DetectionLossConfig DetectionLoss { get; set; } = new();
    }

    /// <summary>
    /// 数据增强配置
    /// </summary>
    public class DataAugmentationConfig
    {
        [JsonProperty("random_horizon_flip")]
        public bool RandomHorizonFlip { get; set; }

        [JsonProperty("random_vertical_flip")]
        public bool RandomVerticalFlip { get; set; }

        [JsonProperty("random_rotation")]
        public bool RandomRotation { get; set; }

        [JsonProperty("random_brightness")]
        public bool RandomBrightness { get; set; }

        [JsonProperty("random_contrast")]
        public bool RandomContrast { get; set; }

        [JsonProperty("random_zoom")]
        public bool RandomZoom { get; set; }
    }

    /// <summary>
    /// 检测任务损失函数配置
    /// </summary>
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
        public float? FocalLossAlpha { get; set; }

        [JsonProperty("focal_loss_gamma")]
        public float? FocalLossGamma { get; set; } = 2.0f;

        [JsonProperty("iou_loss_type")]
        public string IouLossType { get; set; } = "iou"; // "iou", "giou", "diou", "ciou"

        [JsonProperty("mask_weight")]
        public float MaskWeight { get; set; } = 1.0f;

        [JsonProperty("keypoint_weight")]
        public float KeypointWeight { get; set; } = 1.0f;
    }

    /// <summary>
    /// 训练模型配置
    /// </summary>
    public class TrainModel : TrainConfigBase
    {
        // 分类任务配置
        [JsonProperty("classification_config")]
        public ClassificationConfig? Classification { get; set; }

        // 检测任务配置
        [JsonProperty("detection_config")]
        public DetectionConfig? Detection { get; set; }

        /// <summary>
        /// 获取当前任务的配置对象
        /// </summary>
        public object GetTaskConfig()
        {
            return TaskType?.ToLower() switch
            {
                "classification" => Classification ?? new ClassificationConfig(),
                "detection" => Detection ?? new DetectionConfig(),
                _ => Classification ?? new ClassificationConfig()
            };
        }
    }
}
