using System;
using System.Collections.Generic;
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
        public string Scope { get; set; } // 'global', 'group', 'item', 'inventory', 'location'

        public int? GroupId { get; set; }

        public int? ItemId { get; set; }

        public int? InventoryId { get; set; }

        [StringLength(100)]
        public string DefinitionCode { get; set; }

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

        public int? TemplateVersionId { get; set; }

        [Required]
        [StringLength(50)]
        public string ValueType { get; set; } // 'checkbox', 'number', 'select', 'text', 'textarea', 'date', 'datetime'

        [StringLength(50)]
        public string Unit { get; set; }

        public string ValidationRules { get; set; } // JSON string

        [Required]
        [StringLength(20)]
        public string Severity { get; set; } // 'Information', 'Warning', 'Critical'

        [ForeignKey("TemplateVersionId")]
        public virtual ChecklistTemplateVersion TemplateVersion { get; set; }

        public virtual ICollection<ChecklistDefinitionOption> Options { get; set; }

        public ChecklistDefinition()
        {
            IsRequired = true;
            IsActive = true;
            SortOrder = 0;
            CreatedAt = DateTime.Now;
            ValueType = "checkbox";
            Severity = "Information";
            Options = new HashSet<ChecklistDefinitionOption>();
        }
    }
}
