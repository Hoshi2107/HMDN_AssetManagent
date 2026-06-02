using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("CheckCycles")]
    public class CheckCycle
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        public string CycleType { get; set; } // 'daily', 'weekly', 'monthly', 'quarterly', 'yearly'

        [StringLength(200)]
        public string RepeatOn { get; set; }

        public bool IsRepeat { get; set; }

        public int? RepeatCount { get; set; }

        public DateTime? EndDate { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public CheckCycle()
        {
            IsActive = true;
            CreatedAt = DateTime.Now;
            IsRepeat = false;
        }
    }
}
