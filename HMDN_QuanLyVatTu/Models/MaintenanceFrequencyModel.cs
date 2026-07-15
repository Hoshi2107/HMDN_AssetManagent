using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class MaintenanceFrequencyModel
    {
        public string MonthLabel { get; set; }

        // Số lượng ca bảo trì trong tháng đó
        public int MaintenanceCount { get; set; }

        // Số lượng ca sửa chữa trong tháng đó
        public int RepairCount { get; set; }
    }
}