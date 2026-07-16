using System.Collections.Generic;

namespace HMS.Models.Inventory
{
    public class Location
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public string Floor { get; set; }

        public string Building { get; set; }

        public int? DepartmentId { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<Inventory> Inventories { get; set; }

        public Location()
        {
            Inventories = new HashSet<Inventory>();
        }
    }
}