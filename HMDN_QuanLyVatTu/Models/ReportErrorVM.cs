public class ReportErrorVM
{
    public int InventoryId { get; set; }
    public int? TicketId { get; set; }
    public string Title { get; set; }

    public string ErrorDescription { get; set; }

    public string Priority { get; set; }

    public int ReportedBy { get; set; }
}