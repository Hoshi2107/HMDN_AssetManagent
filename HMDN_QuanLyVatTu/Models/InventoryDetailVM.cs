using System;

namespace HMS.Models.ViewModels
{
    public class InventoryDetailVM
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public string SerialNumber { get; set; }

        public int Quantity { get; set; }

        public DateTime ImportDate { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public string LifeStatus { get; set; }

        public string ApprovalStatus { get; set; }

        public string Note { get; set; }

        // ITEM
        public string ItemCode { get; set; }

        public string ItemName { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string Unit { get; set; }

        // GROUP
        public string GroupName { get; set; }

        // LOCATION
        public string LocationName { get; set; }

        public string Floor { get; set; }

        public string Building { get; set; }

        // DEPARTMENT
        public string DepartmentName { get; set; }

        // TICKET
        public string TicketCode { get; set; }

        public string TicketType { get; set; }

        public string TicketStatus { get; set; }

        // USER
        public string CreatedByName { get; set; }

        public string ApprovedByName { get; set; }
    }
}