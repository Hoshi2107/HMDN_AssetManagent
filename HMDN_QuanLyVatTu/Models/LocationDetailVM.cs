namespace HMS.Models.ViewModels
{
    public class LocationDetailVM
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public string Floor { get; set; }

        public string Building { get; set; }

        public string Description { get; set; }

        public int? DepartmentId { get; set; }

        public string DepartmentName { get; set; }

        public bool IsActive { get; set; }
    }
}