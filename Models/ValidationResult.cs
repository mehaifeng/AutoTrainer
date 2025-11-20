using Newtonsoft.Json;
using System.Collections.Generic;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 表示来自 Python 脚本的完整验证结果有效负载。
    /// </summary>
    public class ValidationResult
    {
        [JsonProperty("metrics")]
        public ValidationSummaryMetrics Metrics { get; set; }

        [JsonProperty("chart_data")]
        public ChartData ChartData { get; set; }

        [JsonProperty("image_results")]
        public List<ImageResult> ImageResults { get; set; }
    }

    /// <summary>
    /// 包含所有性能指标。属性可为空，以支持两种任务类型。
    /// </summary>
    public class ValidationSummaryMetrics
    {
        [JsonProperty("time")]
        public double Time { get; set; }

        // 分类指标
        [JsonProperty("accuracy")]
        public double? Accuracy { get; set; }

        [JsonProperty("recall_macro")]
        public double? Recall { get; set; }

        [JsonProperty("f1_macro")]
        public double? F1Score { get; set; }

        // 目标检测指标
        [JsonProperty("map50")]
        public double? Map50 { get; set; }

        [JsonProperty("map50_95")]
        public double? Map50_95 { get; set; }

        [JsonProperty("precision")]
        public double? Precision { get; set; }
    }

    /// <summary>
    /// 存储用于在用户界面中绘制图表的原始数据。
    /// </summary>
    public class ChartData
    {
        [JsonProperty("confusion_matrix")]
        public double[][] ConfusionMatrix { get; set; }

        [JsonProperty("pr_curve_points")]
        public List<PointData> PrCurvePoints { get; set; }

        [JsonProperty("labels")]
        public List<string> Labels { get; set; } // 用于混淆矩阵轴
    }

    /// <summary>
    /// 表示一个具有 X 和 Y 坐标的点。
    /// </summary>
    public class PointData
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }

    /// <summary>
    /// 包含单张图像的验证结果。
    /// </summary>
    public class ImageResult
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        // For classification
        [JsonProperty("predicted_class")]
        public string PredictedClass { get; set; }

        // For detection
        [JsonProperty("predicted_boxes")]
        public List<Box> PredictedBoxes { get; set; }

        [JsonProperty("ground_truth_boxes")]
        public List<Box> GroundTruthBoxes { get; set; }
    }

    /// <summary>
    /// 表示一个带有坐标、标签和置信度的边界框。
    /// </summary>
    public class Box
    {
        // Format: [x_min, y_min, x_max, y_max]
        [JsonProperty("coords")]
        public List<double> Coords { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("confidence")]
        public double? Confidence { get; set; }
    }
}
