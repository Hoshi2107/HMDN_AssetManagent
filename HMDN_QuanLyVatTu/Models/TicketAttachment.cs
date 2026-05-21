using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("TicketAttachments")]
    public class TicketAttachment
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        [StringLength(255)]
        public string FileName { get; set; }

        [StringLength(255)]
        public string StoredName { get; set; }

        public long FileSize { get; set; }

        [StringLength(100)]
        public string MimeType { get; set; }

        public int UploadedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime UploadedAt { get; set; }

        [ForeignKey("TicketId")]
        public virtual Tickets Ticket { get; set; }
    }
}
