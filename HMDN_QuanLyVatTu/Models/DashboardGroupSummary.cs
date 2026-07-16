using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class DashboardGroupSummary
    {
        public string GroupName { get; set; }
        public int Total { get; set; }
        public int Active { get; set; }
        public int Suspended { get; set; }
        public int Disposed { get; set; }
    }
}