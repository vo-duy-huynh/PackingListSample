using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackingListSample
{
    public class PackingListItem
    {
        public string Carton { get; set; }
        public string PONoAndLONo { get; set; }
        public string Description { get; set; }
        public string ColorNo { get; set; }
        public string Batch { get; set; }
        public int QtyCone { get; set; }
        public int TotalQtyCone { get; set; }
        public double GrossWeightPerCTN { get; set; }
        public double NetWeightPerCTN { get; set; }
        public string SizeCTN { get; set; }
        public string MemoNo { get; set; }
    }
}
