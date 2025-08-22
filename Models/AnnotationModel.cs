using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class AnnotationModel
    {
        public string AnnotationGuid { get; } = Guid.NewGuid().ToString();
        public string ImageName { get; set; } = string.Empty;
    }
}
