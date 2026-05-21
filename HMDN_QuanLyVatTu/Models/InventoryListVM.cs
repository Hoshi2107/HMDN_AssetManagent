using Microsoft.SqlServer.Server;

namespace HMS.Models.ViewModels
{
    public class InventoryListVM
    {
        public int Id { get; set; }

        public string AssetCode { get; set; }

        public string ItemName { get; set; }

        public string SerialNumber { get; set; }

        public string DepartmentName { get; set; }

        public string LifeStatus { get; set; }

        public string GroupName { get; set; }

        public string LocationName { get; set; }
    }
}