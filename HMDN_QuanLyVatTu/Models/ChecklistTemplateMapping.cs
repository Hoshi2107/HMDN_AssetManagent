using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistTemplateMappings")]
    public class ChecklistTemplateMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TemplateVersionId { get; set; }

        [Required]
        public int Scope { get; set; } // Enum/numeric ChecklistScope: 1=Global, 2=Category, 3=Asset, 4=Location

        [Required]
        public int TargetId { get; set; } // GroupId, InventoryId, LocationId, etc.

        [Required]
        [StringLength(20)]
        public string CycleType { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [ForeignKey("TemplateVersionId")]
        public virtual ChecklistTemplateVersion TemplateVersion { get; set; }

        public ChecklistTemplateMapping()
        {
            IsActive = true;
        }
    }
}
