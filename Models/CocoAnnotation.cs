using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoTrainer.Models
{
    // 主数据集类
    public class CocoDataset
    {
        [JsonProperty("info")]
        public Info Info { get; set; }

        [JsonProperty("licenses")]
        public List<License> Licenses { get; set; }

        [JsonProperty("images")]
        public List<COCOImage> Images { get; set; }

        [JsonProperty("annotations")]
        public List<Annotation> Annotations { get; set; }

        [JsonProperty("categories")]
        public List<Category> Categories { get; set; }
    }

    // 元信息
    public class Info
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("contributor")]
        public string Contributor { get; set; }

        [JsonProperty("date_created")]
        public string DateCreated { get; set; }
    }

    // 许可信息
    public class License
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    // 图像信息
    [JsonObject("Image")]
    public class COCOImage
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("file_name")]
        public string FileName { get; set; }

        [JsonProperty("license")]
        public int? License { get; set; }

        [JsonProperty("flickr_url")]
        public string FlickrUrl { get; set; }

        [JsonProperty("coco_url")]
        public string CocoUrl { get; set; }

        [JsonProperty("date_captured")]
        public string DateCaptured { get; set; }
    }

    // 类别信息
    public class Category
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("supercategory")]
        public string Supercategory { get; set; }
    }

    // 标注信息（核心）
    public class Annotation
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image_id")]
        public int ImageId { get; set; }

        [JsonProperty("category_id")]
        public int CategoryId { get; set; }

        // 分割：支持 polygon 和 RLE
        [JsonProperty("segmentation")]
        public Segmentation Segmentation { get; set; }

        [JsonProperty("area")]
        public double Area { get; set; }

        [JsonProperty("bbox")]
        public List<double> Bbox { get; set; }  // [x, y, width, height]

        [JsonProperty("iscrowd")]
        public int IsCrowd { get; set; }
    }

    // 分割格式：支持多边形（List<List<double>>）或 RLE（JObject）
    public class Segmentation
    {
        // 多边形：List<List<double>>，每个子列表是一个多边形轮廓
        [JsonProperty("polygons", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<List<double>> Polygons { get; set; }

        // RLE：{"counts": [...], "size": [h, w]}
        [JsonProperty("rle", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JObject Rle { get; set; }

        // 自定义转换器：自动识别类型
        public static Segmentation FromJson(JsonReader reader)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Array)
            {
                // 多边形格式
                var polygons = token.ToObject<List<List<double>>>();
                return new Segmentation { Polygons = polygons };
            }
            else if (token.Type == JTokenType.Object)
            {
                // RLE 格式
                var rle = token.ToObject<JObject>();
                return new Segmentation { Rle = rle };
            }

            throw new JsonSerializationException("Invalid segmentation format");
        }
    }

    // 自定义 JsonConverter 用于 Segmentation
    public class SegmentationConverter : JsonConverter<Segmentation>
    {
        public override Segmentation ReadJson(JsonReader reader, Type objectType, Segmentation existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return Segmentation.FromJson(reader);
        }

        public override void WriteJson(JsonWriter writer, Segmentation value, JsonSerializer serializer)
        {
            if (value.Polygons != null)
            {
                serializer.Serialize(writer, value.Polygons);
            }
            else if (value.Rle != null)
            {
                value.Rle.WriteTo(writer);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}