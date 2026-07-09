using System;
using System.ComponentModel.DataAnnotations.Schema;
using HMS.Models.Catalog;
using HMDN_QuanLyVatTu.Models;

namespace HMS.Models.Inventory
{
    public class Inventory
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public int? ItemId { get; set; }

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

        public string ApprovalNote { get; set; }

        public int? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedQuantity { get; set; }

        public string LifeStatus { get; set; }

        public DateTime? SuspendedAt { get; set; }

        public string SuspendReason { get; set; }

        public DateTime? ActivatedAt { get; set; }

        public int? ReplacedByInventoryId { get; set; }

        public string QrCode { get; set; }
        public string Criticality { get; set; }

        public string Note { get; set; }

        public int CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? IdTicket { get; set; }

        // Navigation
        public virtual Item Item { get; set; }

        public virtual Location Location { get; set; }

        public virtual Department Department { get; set; }

        [ForeignKey("CheckCycleId")]
        public virtual CheckCycle CheckCycle { get; set; }
    }
}