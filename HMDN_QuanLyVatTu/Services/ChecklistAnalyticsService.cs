using System;
using System.Linq;
using System.Data.Entity;
using HMS.Data;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Services
{
    public class ChecklistAnalyticsService
    {
        public class ChecklistProgressMetrics
        {
            public int DoneCount { get; set; }
            public int PendingCount { get; set; }
            public int OverdueCount { get; set; }
            public int TotalSchedules { get; set; }
        }

        public static ChecklistProgressMetrics GetProgressMetrics(HospitalAssetDbContext db, DateTime startRange, DateTime endRange)
        {
            // 1. Lọc các lịch trình checklist trong khoảng thời gian
            var schedulesInRange = db.ChecklistSchedules
                .Where(s => s.ScheduledDate >= startRange && s.ScheduledDate < endRange)
                .Select(s => new
                {
                    s.Id,
                    s.InventoryId,
                    s.LocationId,
                    s.ScheduledDate,
                    s.CycleType,
                    s.Status,
                    s.DueDate
                })
                .ToList()
                .Select(s => new
                {
                    s.Id,
                    s.InventoryId,
                    s.LocationId,
                    s.ScheduledDate,
                    s.CycleType,
                    Status = ((s.Status == "pending" || s.Status == "NeedsReinspection") && s.DueDate < DateTime.Today) ? "overdue" : s.Status
                })
                .ToList();

            // 2. PendingCount & OverdueCount: Đếm số lịch trình chưa làm/trễ hạn (trùng khớp hoàn toàn với logic gộp nhóm và deduplication của GetSchedules)
            var latestPendingDict = schedulesInRange
                .Where(s => s.Status == "pending" || s.Status == "overdue" || s.Status == "NeedsReinspection")
                .GroupBy(s => new { s.InventoryId, s.LocationId, s.CycleType })
                .ToDictionary(
                    g => new { g.Key.InventoryId, g.Key.LocationId, g.Key.CycleType },
                    g => g.OrderByDescending(s => s.ScheduledDate).First().Id
                );

            var resolvedPendingSchedules = schedulesInRange
                .Where(s => s.Status == "pending" || s.Status == "overdue" || s.Status == "NeedsReinspection")
                .Where(s => latestPendingDict.TryGetValue(new { s.InventoryId, s.LocationId, s.CycleType }, out int latestId) && latestId == s.Id)
                .ToList();

            var pendingCount = resolvedPendingSchedules.Count(s => s.Status != "overdue");
            var overdueCount = resolvedPendingSchedules.Count(s => s.Status == "overdue");

            // 3. DoneCount: Đếm số lượng checklist duy nhất thực sự hoàn thành trong khoảng CheckedAt (tránh trùng do gửi nhiều logs cho cùng 1 lịch trình)
            var logsInRange = db.ChecklistLogs
                .Where(l => l.CheckedAt >= startRange && l.CheckedAt < endRange)
                .Select(l => new { l.ScheduleId, l.InventoryId, l.LocationId, l.CycleType })
                .ToList();

            var doneCount = logsInRange
                .Select(l => l.ScheduleId.HasValue 
                    ? "sch_" + l.ScheduleId.Value 
                    : "adhoc_" + l.InventoryId + "_" + l.LocationId + "_" + l.CycleType
                )
                .Distinct()
                .Count();
 
            return new ChecklistProgressMetrics
            {
                DoneCount = doneCount,
                PendingCount = pendingCount,
                OverdueCount = overdueCount,
                TotalSchedules = doneCount + pendingCount + overdueCount
            };
        }
    }
}
