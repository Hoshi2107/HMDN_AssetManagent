using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HMS.Models.Inventory;
using HMS.Models.Auth;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistLogs")]
    public class ChecklistLog
    {
        [Key]
        public int Id { get; set; }

        public int? ScheduleId { get; set; }

        public int? InventoryId { get; set; }

        public int? LocationId { get; set; }

        public int? TemplateVersionId { get; set; }

        [Required]
        public int CheckedBy { get; set; }

        [Required]
        public DateTime CheckedAt { get; set; }

        [Required]
        [StringLength(20)]
        public string CycleType { get; set; }

        [Required]
        [StringLength(20)]
        public string OverallResult { get; set; } // 'pass', 'fail', 'partial'

        [Required]
        [StringLength(20)]
        public string ApprovalStatus { get; set; } // 'Pending', 'Approved'

        [StringLength(2000)]
        public string Note { get; set; }

        public DateTime? QrScannedAt { get; set; }

        [StringLength(200)]
        public string QrLocation { get; set; }

        [StringLength(2000)]
        public string ImageUrls { get; set; }

        [ForeignKey("InventoryId")]
        [JsonIgnore]
        public virtual Inventory Inventory { get; set; }

        [ForeignKey("LocationId")]
        [JsonIgnore]
        public virtual Location Location { get; set; }

        [ForeignKey("TemplateVersionId")]
        [JsonIgnore]
        public virtual ChecklistTemplateVersion TemplateVersion { get; set; }

        [ForeignKey("CheckedBy")]
        [JsonIgnore]
        public virtual User CheckedByUser { get; set; }

        [ForeignKey("ScheduleId")]
        [JsonIgnore]
        public virtual ChecklistSchedule Schedule { get; set; }

        public ChecklistLog()
        {
            CheckedAt = DateTime.Now;
            ApprovalStatus = "Pending";
        }
    }
}

