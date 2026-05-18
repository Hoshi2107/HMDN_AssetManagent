using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class DashboardOverviewModel
    {
        public int TotalAssets { get; set; }
        public int TotalActive { get; set; }
        public int TotalSuspended { get; set; }
        public double ActivePercentage { get; set; }
        public double SuspendedPercentage { get; set; }
    }
}