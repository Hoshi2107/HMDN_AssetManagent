using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using HMS.Data;

namespace HMDN_QuanLyVatTu.Services
{
    public class ChecklistSchedulerService
    {
        private static readonly object _generationLock = new object();

        public void EnsureSchedulesGeneratedForCurrentMonth(HospitalAssetDbContext db)
        {
            DateTime today = DateTime.Today;
            DateTime firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Self-healing: Check if there are any active approved devices with check cycles
            // that do not have any schedules generated in the current month
            bool hasMissingSchedules = db.Inventories.Any(inv =>
                inv.LifeStatus == "active" &&
                inv.ApprovalStatus == "approved" &&
                inv.CheckCycleId != null &&
                (
                    db.ChecklistTemplateMappings.Any(m => m.IsActive &&
                        (
                            (m.Scope == 3 && m.TargetId == inv.Id) ||
                            (m.Scope == 2 && inv.Item != null && m.TargetId == inv.Item.GroupId) ||
                            (m.Scope == 1)
                        )
                    ) ||
                    db.ChecklistDefinitions.Any(cd => cd.IsActive &&
                        (
                            cd.Scope == "global" ||
                            (cd.Scope == "group" && inv.Item != null && cd.GroupId == inv.Item.GroupId) ||
                            (cd.Scope == "item" && cd.ItemId == inv.ItemId) ||
                            (cd.Scope == "inventory" && cd.InventoryId == inv.Id)
                        )
                    )
                ) &&
                !db.ChecklistSchedules.Any(s =>
                    s.InventoryId == inv.Id &&
                    s.ScheduledDate >= firstDayOfMonth &&
                    s.ScheduledDate <= lastDayOfMonth
                )
            );

            if (hasMissingSchedules)
            {
                TriggerGeneration(db, today);
            }
        }

        public void TriggerGeneration(HospitalAssetDbContext db, DateTime targetDate)
        {
            lock (_generationLock)
            {
                try
                {
                    DateTime start = new DateTime(targetDate.Year, targetDate.Month, 1);
                    DateTime end = start.AddMonths(1).AddDays(-1);

                    Trace.WriteLine($"[ChecklistScheduler] Triggering schedule generation from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");

                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_GenerateChecklistSchedules @FromDate, @ToDate",
                        new SqlParameter("@FromDate", start),
                        new SqlParameter("@ToDate", end)
                    );
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"[ChecklistScheduler] Error generating schedules: {ex.Message}. StackTrace: {ex.StackTrace}");
                    throw; // Ném ngược Exception để các API Controller nhận diện và trả về lỗi chuẩn
                }
            }
        }
    }
}
