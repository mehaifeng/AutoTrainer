using AutoTrainer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Extension
{
    public static class AnnotationDictionaryExtensions
    {
        /// <summary>
        /// 将字典序列化为JSON
        /// </summary>
        public static string ToJson(this Dictionary<string, List<AnnotationItem>> dictionary)
        {
            return AnnotationFactory.SerializeToJson(dictionary);
        }

        /// <summary>
        /// 从JSON字符串创建字典
        /// </summary>
        public static Dictionary<string, List<AnnotationItem>> FromJson(this string json)
        {
            return AnnotationFactory.CreateFromJson(json);
        }

        /// <summary>
        /// 保存字典到文件
        /// </summary>
        public static void SaveToFile(this Dictionary<string, List<AnnotationItem>> dictionary, string filePath)
        {
            AnnotationFactory.SaveToFile(dictionary, filePath);
        }

        /// <summary>
        /// 从文件加载字典
        /// </summary>
        public static Dictionary<string, List<AnnotationItem>> LoadFromFile(string filePath)
        {
            return AnnotationFactory.LoadFromFile(filePath);
        }

        /// <summary>
        /// 获取指定类别的所有注解
        /// </summary>
        public static List<AnnotationItem> GetAnnotationsByClass(this Dictionary<string, List<AnnotationItem>> dictionary, string className)
        {
            var result = new List<AnnotationItem>();
            foreach (var list in dictionary.Values)
            {
                result.AddRange(list.Where(a => a.ClassName == className));
            }
            return result;
        }

        /// <summary>
        /// 获取指定类型的注解
        /// </summary>
        public static List<AnnotationItem> GetAnnotationsByType(this Dictionary<string, List<AnnotationItem>> dictionary, Type T)
        {
            var result = new List<AnnotationItem>();
            foreach (var list in dictionary.Values)
            {
                result.AddRange(list.Where(a => a.GetType() == T));
            }
            return result;
        }

        /// <summary>
        /// 添加新的注解到指定类别
        /// </summary>
        public static void AddAnnotation(this Dictionary<string, List<AnnotationItem>> dictionary, string category, AnnotationItem annotation)
        {
            if (!dictionary.ContainsKey(category))
            {
                dictionary[category] = new List<AnnotationItem>();
            }
            dictionary[category].Add(annotation);
        }
    }
}
