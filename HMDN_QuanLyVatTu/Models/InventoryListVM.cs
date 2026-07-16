using Microsoft.SqlServer.Server;

namespace HMS.Models.ViewModels
{
    public class InventoryListVM
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public int ItemId { get; set; }

        public string ItemName { get; set; }
        public string TicketCode { get; set; }
        public string Model { get; set; }

        public string SerialNumber { get; set; }

        public string DepartmentName { get; set; }

        public string LifeStatus { get; set; }

        public string GroupName { get; set; }

        public string LocationName { get; set; }
        public string ApprovalStatus { get; set; }
        public string ReplacedByAssetCode { get; set; }
        public string ErrorTicketTitle { get; set; }
    }
}