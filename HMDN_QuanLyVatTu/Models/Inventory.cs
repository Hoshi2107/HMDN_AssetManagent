using System;
using HMS.Models.Catalog;

namespace HMS.Models.Inventory
{
    public class Inventory
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public int ItemId { get; set; }

        public string SerialNumber { get; set; }

        public int Quantity { get; set; }

        public int? LocationId { get; set; }

        public int? DepartmentId { get; set; }

        public DateTime ImportDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public int? CheckCycleId { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public decimal? DepreciationRate { get; set; }

        public int? DepreciationYears { get; set; }

        public decimal? ResidualValue { get; set; }

        public string ApprovalStatus { get; set; }

        public string LifeStatus { get; set; }

        public string QrCode { get; set; }

        public string Note { get; set; }

        public int CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public virtual Item Item { get; set; }

        public virtual Location Location { get; set; }

        public virtual Department Department { get; set; }
    }
}