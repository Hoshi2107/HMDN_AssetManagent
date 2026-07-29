using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HMS.Models.Inventory;
using HMS.Models.Catalog;
using HMS.Models.Auth;

using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistSchedules")]
    public class ChecklistSchedule
    {
        [Key]
        public int Id { get; set; }

        [StringLength(20)]
        public string TargetType { get; set; } // 'ASSET', 'GROUP', 'LOCATION'

        public int? InventoryId { get; set; }

        public int? GroupId { get; set; }

        public int? LocationId { get; set; }

        public int? TemplateVersionId { get; set; }


        [Required]
        [Column(TypeName = "date")]
        public DateTime ScheduledDate { get; set; }

        [Required]
        [StringLength(20)]
        public string CycleType { get; set; } // 'daily', 'weekly', 'monthly', 'quarterly', 'yearly'

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // 'pending', 'completed', 'skipped'

        [Required]
        [Column(TypeName = "date")]
        public DateTime DueDate { get; set; }

        public int? AssignedTo { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("InventoryId")]
        [JsonIgnore]
        public virtual Inventory Inventory { get; set; }

        [ForeignKey("GroupId")]
        [JsonIgnore]
        public virtual Group Group { get; set; }

        [ForeignKey("LocationId")]
        [JsonIgnore]
        public virtual Location Location { get; set; }

        [ForeignKey("TemplateVersionId")]
        [JsonIgnore]
        public virtual ChecklistTemplateVersion TemplateVersion { get; set; }

        [ForeignKey("AssignedTo")]
        [JsonIgnore]
        public virtual User Assignee { get; set; }

        public ChecklistSchedule()
        {
            TargetType = "ASSET";
            Status = "pending";
            CreatedAt = DateTime.Now;
        }

    }
}

