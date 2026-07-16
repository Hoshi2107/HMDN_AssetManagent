using System;

public class ImportInventoryVM
{
    public string AssetCode { get; set; }
    public int? ItemId { get; set; }
    public string SerialNumber { get; set; }
    public int Quantity { get; set; } = 1;
    public int? DepartmentId { get; set; }
    public int? LocationId { get; set; }
    public DateTime? ImportDate { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public decimal UnitPrice { get; set; }
    public int? YearManufactured { get; set; }
    public int? YearInUse { get; set; }
    public decimal? DepreciationRate { get; set; }
    public int? DepreciationYears { get; set; }
    public string AssetCategory { get; set; }
    public string Manufacturer { get; set; }
    public string SupplierName { get; set; }
    public string CountryManufactured { get; set; }
    public string Note { get; set; }
    public int CreatedBy { get; set; }
}