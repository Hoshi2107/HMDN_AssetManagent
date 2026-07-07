using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("ChecklistLogItems")]
    public class ChecklistLogItem
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int LogId { get; set; }

        [Required]
        public int DefinitionId { get; set; }

        [Required]
        public bool IsPassed { get; set; }

        [StringLength(500)]
        public string Note { get; set; }

        public decimal? NumericValue { get; set; }

        public string StringValue { get; set; }

        [ForeignKey("LogId")]
        [JsonIgnore]
        public virtual ChecklistLog Log { get; set; }

        [ForeignKey("DefinitionId")]
        [JsonIgnore]
        public virtual ChecklistDefinition Definition { get; set; }
    }
}

