using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class MaintenanceLog
    {
        public int Id { get; set; }

        public int InventoryId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ErrorDescription { get; set; }

        public string ActionTaken { get; set; }

        public decimal? Cost { get; set; } // Cột lõi lấy chi phí bảo trì

        public string PartReplaced { get; set; }

        public string Vendor { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string Status { get; set; } = "open";

        public string Priority { get; set; } = "normal";

        public int? AssignedTo { get; set; }

        public int ReportedBy { get; set; }

        public string ImageUrls { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? ClosedBy { get; set; }

        public DateTime? ClosedAt { get; set; }

    }
}