using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class DashboardOverviewModel
    {
        public int TotalAssets { get; set; }
        public int OperatingWell { get; set; }
        public int BrokenAssets { get; set; }
        public double ActivePercentage { get; set; }
        public double BrokenPercentage { get; set; }
        public string MaintenanceDeviceName { get; set; }
        public int HospitalMaintenanceCount { get; set; }
        public int VendorMaintenanceCount { get; set; }
    }
}