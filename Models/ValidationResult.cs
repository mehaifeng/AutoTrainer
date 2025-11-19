using Newtonsoft.Json;
using System.Collections.Generic;

namespace AutoTrainer.Models
{
    /// <summary>
    /// Represents the complete validation result payload from a Python script.
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
    /// Holds all performance metrics. Properties are nullable to support both task types.
    /// </summary>
    public class ValidationSummaryMetrics
    {
        [JsonProperty("time")]
        public double Time { get; set; }

        // Classification metrics
        [JsonProperty("accuracy")]
        public double? Accuracy { get; set; }

        [JsonProperty("recall_macro")]
        public double? Recall { get; set; }

        [JsonProperty("f1_macro")]
        public double? F1Score { get; set; }

        // Detection metrics
        [JsonProperty("map50")]
        public double? Map50 { get; set; }

        [JsonProperty("map50_95")]
        public double? Map50_95 { get; set; }

        [JsonProperty("precision")]
        public double? Precision { get; set; }
    }

    /// <summary>
    /// Holds the raw data required for plotting charts in the UI.
    /// </summary>
    public class ChartData
    {
        [JsonProperty("confusion_matrix")]
        public double[][] ConfusionMatrix { get; set; }

        [JsonProperty("pr_curve_points")]
        public List<PointData> PrCurvePoints { get; set; }

        [JsonProperty("labels")]
        public List<string> Labels { get; set; } // For confusion matrix axes
    }

    /// <summary>
    /// Represents a point with X and Y coordinates.
    /// </summary>
    public class PointData
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }

    /// <summary>
    /// Contains the validation result for a single image.
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
    /// Represents a bounding box with coordinates, label, and confidence.
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
