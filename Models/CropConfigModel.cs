using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class CropConfigModel
    {
        public string Name { get; set; }
        public List<SingleCropArea> Coprs { get; set; } = [];
    }
    public class SingleCropArea
    {
        public string Name { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
    }
}
