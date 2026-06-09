using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("Tickets")]
    public class Tickets
    {
        [Key]
        public int Id { get; set; }

        [StringLength(50)]
        public string TicketCode { get; set; }

        [StringLength(20)]
        public string TicketType { get; set; }

        [StringLength(20)]
        public string Status { get; set; }

        public string Note { get; set; }

        public int CreatedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }

        public int? CheckedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? CheckedAt { get; set; }

        public int? ApprovedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ApprovedAt { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? TransactionDate { get; set; }

        public int? SendTo { get; set; }

        [StringLength(255)]
        public string Title { get; set; }
    }
}
