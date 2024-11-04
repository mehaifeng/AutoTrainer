using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class CorpConfigModel
    {
        public string Name { get; set; }
        public List<SingleCorpArea> Coprs { get; set; } = [];
    }
    public class SingleCorpArea
    {
        public string Name { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
    }
}
