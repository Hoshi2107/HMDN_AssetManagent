using System;
using System.Collections.Generic;

namespace HMS.Models.Catalog
{
    public class Group
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public string Icon { get; set; }

        public int? DefaultCycleId { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Item> Items { get; set; }

        public Group()
        {
            Items = new HashSet<Item>();
        }
    }
}