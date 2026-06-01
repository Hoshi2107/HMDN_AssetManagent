using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Models.Inventory
{
    [Table("InventoryAttachments")]
    public class InventoryAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FileName { get; set; }

        [MaxLength(50)]
        public string FileType { get; set; }

        public long FileSize { get; set; }

        [Required]
        public byte[] FileData { get; set; }

        public int? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
