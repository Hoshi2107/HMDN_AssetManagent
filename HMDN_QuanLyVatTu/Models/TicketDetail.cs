using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMDN_QuanLyVatTu.Models
{
    [Table("TicketDetails")]
    public class TicketDetail
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        [StringLength(255)]
        public string ItemName { get; set; }

        [StringLength(50)]
        public string Unit { get; set; }

        public int Quantity { get; set; }

        public string Note { get; set; }

        [StringLength(50)]
        public string ApprovalStatus { get; set; }

        public int? ApprovedQuantity { get; set; }

        public string ApprovalNote { get; set; }
    }
}
