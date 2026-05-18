using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class InventoryReportModel
    {
        public int Id { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
        public string GroupName { get; set; }
        public string DepartmentName { get; set; }
        public string LocationName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string LifeStatus { get; set; }

        public class FilterLookupModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}