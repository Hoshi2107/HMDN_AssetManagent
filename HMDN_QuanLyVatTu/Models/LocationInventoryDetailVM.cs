public class LocationInventoryDetailVM
{
    // LOCATION
    public int LocationId { get; set; }

    public string LocationCode { get; set; }

    public string LocationName { get; set; }

    public string Floor { get; set; }

    public string Building { get; set; }

    public string DepartmentName { get; set; }

    // INVENTORY
    public int? InventoryId { get; set; }

    public string AssetCode { get; set; }

    public string SerialNumber { get; set; }

    public int? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? TotalPrice { get; set; }

    public string LifeStatus { get; set; }
    
    public string LifeStatusText { get; set; }

    public string ApprovalStatus { get; set; }

    // ITEM
    public int? ItemId { get; set; }

    public string ItemCode { get; set; }

    public string ItemName { get; set; }

    public string Brand { get; set; }

    public string Model { get; set; }

    public string Unit { get; set; }

    // GROUP
    public string GroupName { get; set; }

    public string GroupIcon { get; set; }
}