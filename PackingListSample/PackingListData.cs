using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackingListSample
{
    public class PackingListData
    {
        public string CustomerName { get; set; }
        public string CustomerNo { get; set; }
        public DateTime ShippingDate { get; set; }
        public string ShippingAddress { get; set; }
        public string PackingListNo { get; set; }

        public int TotalQuantity { get; set; }
        public int TotalPackings { get; set; }
        public double NetWeight { get; set; }
        public double GrossWeight { get; set; }
        public double Measurement { get; set; }
    }
}
