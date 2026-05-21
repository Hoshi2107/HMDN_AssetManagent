using System;

namespace HMS.Models.ViewModels
{
    public class ItemInventoryVM
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public string SerialNumber { get; set; }

        public int Quantity { get; set; }

        public DateTime? ImportDate { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public decimal TotalPrice { get; set; }

        public string LifeStatus { get; set; }

        public string LocationName { get; set; }
    }
}