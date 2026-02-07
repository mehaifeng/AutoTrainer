using AutoTrainer.Enums;
using AutoTrainer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Extensions
{
    public static class AnnotationFactory
    {
        private static readonly Dictionary<AnnotationToolEnum, Type> _typeMap = new Dictionary<AnnotationToolEnum, Type>
    {
        { AnnotationToolEnum.Rectangle, typeof(RectangleModel) },
        { AnnotationToolEnum.Polygon, typeof(PolygonModel) },
        { AnnotationToolEnum.Point, typeof(PointModel) }
    };

        /// <summary>
        /// 从JSON反序列化整个字典
        /// </summary>
        /// <param name="json">包含字典的JSON字符串</param>
        /// <returns>反序列化后的字典</returns>
        public static Dictionary<string, List<AnnotationItem>> CreateFromJson(string json)
        {
            try
            {
                // 先解析为JObject，这样可以逐个处理每个列表
                JObject jObject = JObject.Parse(json);
                var result = new Dictionary<string, List<AnnotationItem>>();

                foreach (var property in jObject)
                {
                    string key = property.Key;
                    JToken? value = property.Value;

                    if (value != null && value.Type == JTokenType.Array)
                    {
                        result[key] = DeserializeAnnotationList((JArray)value);
                    }
                    else
                    {
                        throw new JsonSerializationException($"键 '{key}' 的值不是数组类型");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException($"反序列化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将字典序列化为JSON字符串
        /// </summary>
        /// <param name="dictionary">要序列化的字典</param>
        /// <returns>JSON字符串</returns>
        public static string SerializeToJson(Dictionary<string, List<AnnotationItem>> dictionary)
        {
            try
            {
                // 直接序列化，因为每个具体类型都能被正确处理
                return JsonConvert.SerializeObject(dictionary, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                });
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException($"序列化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从文件加载注解字典
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的字典</returns>
        public static Dictionary<string, List<AnnotationItem>> LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件未找到: {filePath}");

            string json = File.ReadAllText(filePath);
            return CreateFromJson(json);
        }

        /// <summary>
        /// 保存注解字典到文件
        /// </summary>
        /// <param name="dictionary">要保存的字典</param>
        /// <param name="filePath">文件路径</param>
        public static void SaveToFile(Dictionary<string, List<AnnotationItem>> dictionary, string filePath)
        {
            string json = SerializeToJson(dictionary);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 获取支持的类型
        /// </summary>
        public static IEnumerable<AnnotationToolEnum> GetSupportedTypes()
        {
            return _typeMap.Keys;
        }

        // 私有方法：反序列化单个列表
        private static List<AnnotationItem> DeserializeAnnotationList(JArray jArray)
        {
            var list = new List<AnnotationItem>();

            foreach (JObject item in jArray)
            {
                // 提取AnnotationType
                if (!item.TryGetValue("AnnotationType", out JToken? typeToken))
                {
                    throw new JsonSerializationException("注解项缺少AnnotationType字段");
                }

                AnnotationToolEnum annotationType;
                if (typeToken.Type == JTokenType.Integer)
                {
                    annotationType = (AnnotationToolEnum)typeToken.Value<int>();
                }
                else
                {
                    if (!Enum.TryParse<AnnotationToolEnum>(typeToken.Value<string>(), out annotationType))
                    {
                        throw new JsonSerializationException($"无法解析AnnotationType: {typeToken}");
                    }
                }

                // 获取具体类型
                if (!_typeMap.TryGetValue(annotationType, out Type? targetType))
                {
                    throw new JsonSerializationException($"不支持的注解类型: {annotationType}");
                }

                // 反序列化为具体类型
                var annotation = JsonConvert.DeserializeObject(item.ToString(), targetType) as AnnotationItem;
                if (annotation != null)
                {
                    list.Add(annotation);
                }
            }

            return list;
        }
    }
}
