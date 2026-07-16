using System;
public class UpdateScheduleDto
{
    public int Id { get; set; }
    public string ScheduleName { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurringMonths { get; set; }
}