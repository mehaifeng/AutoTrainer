using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public enum AnnotationMode
    {
        Manual,     // 手动标注
        Template,   // 模板标注
        AIAssisted  // AI辅助标注
    }
}
