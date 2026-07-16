using System;

namespace HMS.Models.ViewModels
{
    public class ItemVM
    {
        public int Id { get; set; }

        public int GroupId { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string Unit { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int TotalQuantity { get; set; }
    }
}