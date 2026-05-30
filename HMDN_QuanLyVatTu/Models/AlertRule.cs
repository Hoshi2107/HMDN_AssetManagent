using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("AlertRules")]
    public class AlertRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; }

        [Required]
        [StringLength(300)]
        [Column(TypeName = "nvarchar")]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string AlertType { get; set; }

        public int? ThresholdDays { get; set; }

        public int? ThresholdCount { get; set; }

        public int? ThresholdPeriodDays { get; set; }

        public bool IsActive { get; set; }

        [StringLength(500)]
        [Column(TypeName = "nvarchar")]
        public string Description { get; set; }

        // Navigation
        [JsonIgnore]
        public virtual ICollection<Alert> Alerts { get; set; }

        public AlertRule()
        {
            Alerts = new HashSet<Alert>();
            IsActive = true;
        }
    }
}
