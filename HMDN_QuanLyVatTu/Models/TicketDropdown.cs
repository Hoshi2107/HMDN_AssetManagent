using System;

namespace HMS.Models.ViewModels
{
    public class TicketDropdownVM
    {
        public int Id { get; set; }

        public string TicketCode { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

}
