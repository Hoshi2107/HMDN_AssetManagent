using System;

public class RenewalCreateDto
{
    public string RenewalName { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; } = 7;
    public bool IsRecurring { get; set; }
    public int? RecurringMonths { get; set; }
    public string Note { get; set; }
}