using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class ClassifyModel
    {
        [JsonProperty("image_path")]
        public string? imagePath { get; set; }
        [JsonProperty("predicted_class")]
        public string? predictedClass { get; set; }
        [JsonProperty("confidence")]
        public double confidence { get; set; }
    }
}
