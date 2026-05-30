using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HMS.Models.Inventory;
using HMS.Models.Auth;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("Alerts")]
    public class Alert
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int AlertRuleId { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        [StringLength(300)]
        [Column(TypeName = "nvarchar")]
        public string Title { get; set; }

        [StringLength(1000)]
        [Column(TypeName = "nvarchar")]
        public string Body { get; set; }

        [Required]
        [StringLength(10)]
        public string Severity { get; set; } // 'info', 'warning', 'danger'

        public bool IsResolved { get; set; }

        public int? ResolvedBy { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsNotified { get; set; }

        // Navigation
        [ForeignKey("AlertRuleId")]
        [JsonIgnore]
        public virtual AlertRule AlertRule { get; set; }

        [ForeignKey("InventoryId")]
        [JsonIgnore]
        public virtual Inventory Inventory { get; set; }

        [ForeignKey("ResolvedBy")]
        [JsonIgnore]
        public virtual User Resolver { get; set; }

        public Alert()
        {
            CreatedAt = DateTime.Now;
            IsResolved = false;
            IsNotified = false;
            Severity = "warning";
        }
    }
}
