namespace HMDN.Models.Location
{
    public class UpdateLocationVM
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Floor { get; set; }

        public string Building { get; set; }

        public int? DepartmentId { get; set; }
    }
}