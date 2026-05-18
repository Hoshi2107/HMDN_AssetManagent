using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    public class DepreciationLog
    {
        public int Id { get; set; }

        public int InventoryId { get; set; }

        public int Year { get; set; }

        public decimal OpeningValue { get; set; }

        public decimal DepreciationAmt { get; set; }

        public decimal ClosingValue { get; set; }

        public string Note { get; set; }

        public DateTime CalculatedAt { get; set; }

        public int CalculatedBy { get; set; }

        [ForeignKey("InventoryId")]
        public virtual HMS.Models.Inventory.Inventory Inventory { get; set; }
    }
}
