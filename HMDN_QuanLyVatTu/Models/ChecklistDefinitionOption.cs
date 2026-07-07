using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistDefinitionOptions")]
    public class ChecklistDefinitionOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ChecklistDefinitionId { get; set; }

        [Required]
        [StringLength(100)]
        public string Value { get; set; }

        [Required]
        [StringLength(200)]
        public string DisplayText { get; set; }

        [StringLength(50)]
        public string Color { get; set; }

        [Required]
        public int SortOrder { get; set; }

        [Required]
        public bool IsDefault { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [ForeignKey("ChecklistDefinitionId")]
        [JsonIgnore]
        public virtual ChecklistDefinition ChecklistDefinition { get; set; }

        public ChecklistDefinitionOption()
        {
            SortOrder = 0;
            IsDefault = false;
            IsActive = true;
        }
    }
}
