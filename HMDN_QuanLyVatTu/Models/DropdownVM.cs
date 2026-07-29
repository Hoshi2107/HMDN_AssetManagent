namespace HMS.Models.ViewModels
{
    public class DropdownVM
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public class ItemDropdownVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
    }
}