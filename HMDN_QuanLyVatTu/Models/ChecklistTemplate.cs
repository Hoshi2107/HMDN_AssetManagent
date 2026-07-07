using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistTemplates")]
    public class ChecklistTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<ChecklistTemplateVersion> Versions { get; set; }

        public ChecklistTemplate()
        {
            CreatedAt = DateTime.Now;
            Versions = new HashSet<ChecklistTemplateVersion>();
        }
    }
}
