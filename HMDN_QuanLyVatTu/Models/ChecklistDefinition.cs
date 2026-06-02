using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistDefinitions")]
    public class ChecklistDefinition
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Scope { get; set; } // 'global', 'group', 'item'

        public int? GroupId { get; set; }

        public int? ItemId { get; set; }

        [StringLength(20)]
        public string CycleType { get; set; } // 'daily', 'weekly', 'monthly', 'yearly', null (any)

        [Required]
        [StringLength(300)]
        public string CheckName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsRequired { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public ChecklistDefinition()
        {
            IsRequired = true;
            IsActive = true;
            SortOrder = 0;
            CreatedAt = DateTime.Now;
        }
    }
}
