using Newtonsoft.Json;
using System.Collections.Generic;

namespace AutoTrainer.Models
{
    /// <summary>
    /// ��ʾ���� Python �ű���������֤�����Ч���ء�
    /// </summary>
    public class ValidationResult
    {
        public ValidationResult()
        {
            Metrics = new ValidationSummaryMetrics();
            ChartData = new ChartData();
            ImageResults = new List<ImageResult>();
        }

        [JsonProperty("metrics")]
        public ValidationSummaryMetrics Metrics { get; set; }

        [JsonProperty("chart_data")]
        public ChartData ChartData { get; set; }

        [JsonProperty("image_results")]
        public List<ImageResult> ImageResults { get; set; }
    }

    /// <summary>
    /// ������������ָ�ꡣ���Կ�Ϊ�գ���֧�������������͡�
    /// </summary>
    public class ValidationSummaryMetrics
    {
        [JsonProperty("time")]
        public double Time { get; set; }

        // ����ָ��
        [JsonProperty("accuracy")]
        public double? Accuracy { get; set; }

        [JsonProperty("recall_macro")]
        public double? Recall { get; set; }

        [JsonProperty("f1_macro")]
        public double? F1Score { get; set; }

        // Ŀ����ָ��
        [JsonProperty("map50")]
        public double? Map50 { get; set; }

        [JsonProperty("map50_95")]
        public double? Map50_95 { get; set; }

        [JsonProperty("precision")]
        public double? Precision { get; set; }
    }

    /// <summary>
    /// �洢�������û������л���ͼ����ԭʼ���ݡ�
    /// </summary>
    public class ChartData
    {
        public ChartData()
        {
            ConfusionMatrix = new double[0][];
            PrCurvePoints = new List<PointData>();
            Labels = new List<string>();
        }

        [JsonProperty("confusion_matrix")]
        public double[][] ConfusionMatrix { get; set; }

        [JsonProperty("pr_curve_points")]
        public List<PointData> PrCurvePoints { get; set; }

        [JsonProperty("labels")]
        public List<string> Labels { get; set; } // ���ڻ���������
    }

    /// <summary>
    /// ��ʾһ������ X �� Y ����ĵ㡣
    /// </summary>
    public class PointData
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }

    /// <summary>
    /// ��������ͼ�����֤�����
    /// </summary>
    public class ImageResult
    {
        public ImageResult()
        {
            Path = string.Empty;
            PredictedClass = string.Empty;
            PredictedBoxes = new List<Box>();
            GroundTruthBoxes = new List<Box>();
        }

        [JsonProperty("path")]
        public string Path { get; set; }

        // For classification
        [JsonProperty("predicted_class")]
        public string PredictedClass { get; set; }

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        // For detection
        [JsonProperty("predicted_boxes")]
        public List<Box> PredictedBoxes { get; set; }

        [JsonProperty("ground_truth_boxes")]
        public List<Box> GroundTruthBoxes { get; set; }
    }

    /// <summary>
    /// ��ʾһ���������ꡢ��ǩ�����Ŷȵı߽��
    /// </summary>
    public class Box
    {
        public Box()
        {
            Coords = new List<double>();
            Label = string.Empty;
        }

        // Format: [x_min, y_min, x_max, y_max]
        [JsonProperty("coords")]
        public List<double> Coords { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("confidence")]
        public double? Confidence { get; set; }
    }
}
