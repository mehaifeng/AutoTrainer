using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class ModelQuality
    {
        /// <summary>
        /// 准确率
        /// </summary>
        public double Accuracy {  get; set; }
        /// <summary>
        /// 精确率
        /// </summary>
        public double Precision { get; set; }
        /// <summary>
        /// 召回率
        /// </summary>
        public double Recall { get; set; }
        /// <summary>
        /// F1值
        /// </summary>
        public double F1_score {  get; set; } 
    }
}
