using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    [JsonObject("loss_function")]
    public class LossFunctionModel
    {
        [JsonProperty("type")]
        public string? type { get; set; }
        [JsonProperty("args")]
        public Params? args { get; set; }
    }
    [JsonObject("args")]
    public class Params
    {
        [JsonProperty("weight")]
        public double[]? weight { get; set; }
        [JsonProperty("pos_weight")]
        public double[]? pos_weight { get; set; }
        [JsonProperty("reduction")]
        public string? reduction { get; set; }
        [JsonProperty("beta")]
        public double? Beta { get; set; }
        [JsonProperty("label_smoothing")]
        public double? label_smoothing { get; set; }
    }
}
