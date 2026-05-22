using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("TicketDiscussions")]
    public class TicketDiscussion
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        [Required]
        [StringLength(100)]
        public string SenderName { get; set; }

        public string Message { get; set; }

        [StringLength(500)]
        public string FilePath { get; set; }

        [StringLength(255)]
        public string FileName { get; set; }

        [StringLength(50)]
        public string FileType { get; set; } // "TEXT", "IMAGE", "FILE"

        public bool IsRevoked { get; set; } = false;

        public DateTime CreatedAt { get; set; }

        [ForeignKey("TicketId")]
        public virtual Tickets Ticket { get; set; }
    }
}
