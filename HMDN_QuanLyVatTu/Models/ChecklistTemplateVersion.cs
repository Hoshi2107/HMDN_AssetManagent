using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistTemplateVersions")]
    public class ChecklistTemplateVersion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TemplateId { get; set; }

        [Required]
        public int Version { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime EffectiveFrom { get; set; }

        [Column(TypeName = "date")]
        public DateTime? EffectiveTo { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("TemplateId")]
        public virtual ChecklistTemplate Template { get; set; }

        public virtual ICollection<ChecklistTemplateMapping> Mappings { get; set; }
        public virtual ICollection<ChecklistDefinition> Definitions { get; set; }

        public ChecklistTemplateVersion()
        {
            Version = 1;
            IsActive = true;
            CreatedAt = DateTime.Now;
            Mappings = new HashSet<ChecklistTemplateMapping>();
            Definitions = new HashSet<ChecklistDefinition>();
        }
    }
}
