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

            string sqlCheck = @"
                WITH ApplicableCycles AS (
                    -- 1. Default CheckCycle assigned to the device
                    SELECT 
                        inv.Id AS InventoryId,
                        LOWER(cy.CycleType) AS CycleType
                    FROM Inventory inv
                    JOIN CheckCycles cy ON inv.CheckCycleId = cy.Id
                    WHERE inv.LifeStatus = 'active'
                      AND inv.ApprovalStatus = 'approved'

                    UNION

                    -- 2. Cycles from active checklist definitions applicable to the device
                    SELECT DISTINCT
                        inv.Id AS InventoryId,
                        LOWER(cd.CycleType) AS CycleType
                    FROM Inventory inv
                    JOIN Items it ON inv.ItemId = it.Id
                    JOIN ChecklistDefinitions cd ON cd.IsActive = 1 
                        AND cd.CycleType IS NOT NULL
                        AND (
                            cd.Scope = 'global'
                            OR (cd.Scope = 'group' AND cd.GroupId = it.GroupId)
                            OR (cd.Scope = 'item' AND cd.ItemId = inv.ItemId)
                            OR (cd.Scope = 'inventory' AND cd.InventoryId = inv.Id)
                        )
                    WHERE inv.LifeStatus = 'active'
                      AND inv.ApprovalStatus = 'approved'
                )
                SELECT TOP 1 1 
                FROM ApplicableCycles ac
                WHERE NOT EXISTS (
                    SELECT 1 FROM ChecklistSchedules s
                    WHERE s.InventoryId = ac.InventoryId
                      AND LOWER(s.CycleType) = ac.CycleType
                      AND s.ScheduledDate >= @FirstDay
                      AND s.ScheduledDate <= @LastDay
                );";

            bool hasMissingSchedules = db.Database.SqlQuery<int>(
                sqlCheck,
                new SqlParameter("@FirstDay", firstDayOfMonth),
                new SqlParameter("@LastDay", lastDayOfMonth)
            ).Any();

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
