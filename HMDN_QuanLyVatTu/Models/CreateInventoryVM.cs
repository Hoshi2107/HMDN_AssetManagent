using System;

namespace HMS.Models.ViewModels
{
    public class CreateInventoryVM
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public int ItemId { get; set; }

        public string SerialNumber { get; set; }

        public int Quantity { get; set; }

        public int? DepartmentId { get; set; }

        public int? LocationId { get; set; }

        public DateTime? ImportDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public int? CheckCycleId { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal? DepreciationRate { get; set; }

        public int? DepreciationYears { get; set; }

        public decimal? ResidualValue { get; set; }

        public int? ApprovedQuantity { get; set; }

        public int? YearManufactured { get; set; }

        public int? YearInUse { get; set; }

        public int? UsageYears { get; set; }

        public string AssetCategory { get; set; }

        public string GroupAssetCode { get; set; }

        public string AccountingCode { get; set; }

        public string InsuranceCode { get; set; }

        public string CountryManufactured { get; set; }

        public string Manufacturer { get; set; }

        public string SupplierName { get; set; }

        public string QrCode { get; set; }

        public string Note { get; set; }

        public int CreatedBy { get; set; }

        public int? IdTicket { get; set; }
    }
}