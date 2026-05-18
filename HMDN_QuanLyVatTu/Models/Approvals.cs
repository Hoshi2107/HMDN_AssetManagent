using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    /// <summary>
    /// Represents an approval record for an asset.
    /// </summary>
    public class Approvals
    {
        [Key]
        public int Id { get; set; }

        // Foreign key to the asset being approved (assuming an Inventory entity exists)
        public int AssetId { get; set; }

        // Name or identifier of the user who approved
        [Required]
        [StringLength(100)]
        public string ApprovedBy { get; set; }

        // Date and time when the approval was made
        public DateTime ApprovedDate { get; set; }

        // Current status of the approval (e.g., Pending, Approved, Rejected)
        [Required]
        [StringLength(50)]
        public string Status { get; set; }

        // Optional comments from the approver
        [StringLength(500)]
        public string Comments { get; set; }
    }
}
