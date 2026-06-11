using System;

public class MaintenanceScheduleVM
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public string ScheduleName { get; set; }
    public string MaintenanceType { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurringMonths { get; set; }
    public string Status { get; set; }
    public string CreatedByName { get; set; }
    public int DaysUntilDue { get; set; }
}