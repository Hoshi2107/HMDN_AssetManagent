namespace HMDN.Models.Location
{
    public class CreateLocationVM
    {
        public string Name { get; set; }

        public string Floor { get; set; }

        public string Building { get; set; }

        public int? DepartmentId { get; set; }
    }
}