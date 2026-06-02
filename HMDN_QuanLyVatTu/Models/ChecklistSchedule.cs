using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HMS.Models.Inventory;
using HMS.Models.Auth;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistSchedules")]
    public class ChecklistSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

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

        [ForeignKey("AssignedTo")]
        [JsonIgnore]
        public virtual User Assignee { get; set; }

        public ChecklistSchedule()
        {
            Status = "pending";
            CreatedAt = DateTime.Now;
        }
    }
}
