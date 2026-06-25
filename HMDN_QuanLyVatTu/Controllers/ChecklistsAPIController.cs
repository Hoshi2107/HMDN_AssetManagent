using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;
using HMS.Data;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/checklists")]
    [CustomApiAuthorize("Checklists")]
    public class ChecklistsApiController : ApiController
    {
        private HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET: api/checklists/schedules
        [HttpGet]
        [Route("schedules")]
        [Route("~/api/checklist/schedules")]
        public IHttpActionResult GetSchedules(string fromDate = null, string toDate = null, string status = null, string cycleType = null)
        {
            try
            {
                // Self-healing: If ChecklistSchedules table is completely empty, auto-generate for the current month
                if (!db.ChecklistSchedules.Any())
                {
                    DateTime today = DateTime.Today;
                    DateTime start = new DateTime(today.Year, today.Month, 1);
                    DateTime end = start.AddMonths(1).AddDays(-1);

                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_GenerateChecklistSchedules @FromDate, @ToDate",
                        new SqlParameter("@FromDate", start),
                        new SqlParameter("@ToDate", end)
                    );
                }

                var query = db.ChecklistSchedules.AsQueryable();

                if (!string.IsNullOrEmpty(fromDate))
                {
                    DateTime start = DateTime.Parse(fromDate, System.Globalization.CultureInfo.InvariantCulture);
                    query = query.Where(s => s.ScheduledDate >= start);
                }
                if (!string.IsNullOrEmpty(toDate))
                {
                    DateTime end = DateTime.Parse(toDate, System.Globalization.CultureInfo.InvariantCulture).Date.AddDays(1);
                    query = query.Where(s => s.ScheduledDate < end);
                }
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "pending")
                    {
                        query = query.Where(s => s.Status == "pending" || s.Status == "NeedsReinspection");
                    }
                    else
                    {
                        query = query.Where(s => s.Status == status);
                    }
                }

                if (!string.IsNullOrEmpty(cycleType))
                {
                    query = query.Where(s => s.CycleType == cycleType);
                }

                var openRepairInventoryIds = db.MaintenanceLogs
                    .Where(ml => ml.Status == "open" || ml.Status == "in_progress")
                    .Select(ml => ml.InventoryId)
                    .Distinct()
                    .ToList();

                var resolvedSchedules = query
                    .OrderBy(s => s.ScheduledDate)
                    .ThenBy(s => s.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.InventoryId,
                        AssetCode = s.Inventory != null ? s.Inventory.AssetCode : "",
                        ItemName = (s.Inventory != null && s.Inventory.Item != null) ? s.Inventory.Item.Name : "N/A",
                        SerialNumber = s.Inventory != null ? s.Inventory.SerialNumber : "",
                        DepartmentName = (s.Inventory != null && s.Inventory.Department != null) ? s.Inventory.Department.Name : "",
                        QrCode = s.Inventory != null ? s.Inventory.QrCode : "",
                        ScheduledDate = s.ScheduledDate,
                        s.CycleType,
                        s.Status,
                        s.DueDate,
                        s.AssignedTo,
                        AssigneeName = s.Assignee != null ? s.Assignee.FullName : "",
                        GroupId = (s.Inventory != null && s.Inventory.Item != null && s.Inventory.Item.Group != null) 
                            ? s.Inventory.Item.Group.Id : 0,
                        GroupCode = (s.Inventory != null && s.Inventory.Item != null && s.Inventory.Item.Group != null) 
                            ? s.Inventory.Item.Group.Code : "",
                        GroupName = (s.Inventory != null && s.Inventory.Item != null && s.Inventory.Item.Group != null) 
                            ? s.Inventory.Item.Group.Name : "Chưa phân nhóm",
                        GroupIcon = (s.Inventory != null && s.Inventory.Item != null && s.Inventory.Item.Group != null) 
                            ? s.Inventory.Item.Group.Icon : "📦",
                        LifeStatus = s.Inventory != null ? s.Inventory.LifeStatus : "active",
                        Criticality = s.Inventory != null ? s.Inventory.Criticality : "Low"
                    })
                    .ToList()
                    .Select(s => new
                    {
                        s.Id,
                        s.InventoryId,
                        s.AssetCode,
                        s.ItemName,
                        s.SerialNumber,
                        s.DepartmentName,
                        s.QrCode,
                        s.ScheduledDate,
                        s.CycleType,
                        Status = ((s.Status == "pending" || s.Status == "NeedsReinspection") && s.DueDate < DateTime.Today) ? "overdue" : s.Status,
                        OriginalStatus = s.Status,
                        DueDate = s.DueDate,
                        s.AssignedTo,
                        s.AssigneeName,
                        s.GroupId,
                        s.GroupCode,
                        s.GroupName,
                        s.GroupIcon,
                        s.LifeStatus,
                        Criticality = string.IsNullOrEmpty(s.Criticality) ? "Low" : s.Criticality,
                        HasOpenRepair = openRepairInventoryIds.Contains(s.InventoryId)
                    })
                    .ToList();

                var latestPendingDict = resolvedSchedules
                    .Where(s => s.Status == "pending" || s.Status == "overdue" || s.Status == "NeedsReinspection")
                    .GroupBy(s => new { s.InventoryId, s.CycleType })
                    .ToDictionary(
                        g => new { g.Key.InventoryId, g.Key.CycleType },
                        g => g.OrderByDescending(s => s.ScheduledDate).First().Id
                    );

                var schedules = resolvedSchedules
                    .Where(s => (s.Status != "pending" && s.Status != "overdue" && s.Status != "NeedsReinspection") || latestPendingDict[new { s.InventoryId, s.CycleType }] == s.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.InventoryId,
                        s.AssetCode,
                        s.ItemName,
                        s.SerialNumber,
                        s.DepartmentName,
                        s.QrCode,
                        ScheduledDate = s.ScheduledDate.ToString("yyyy-MM-dd"),
                        s.CycleType,
                        s.Status,
                        s.OriginalStatus,
                        DueDate = s.DueDate.ToString("yyyy-MM-dd"),
                        s.AssignedTo,
                        s.AssigneeName,
                        s.GroupId,
                        s.GroupCode,
                        s.GroupName,
                        s.GroupIcon,
                        s.LifeStatus,
                        s.Criticality,
                        s.HasOpenRepair
                    })
                    .ToList();

                return Ok(new { success = true, data = schedules });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/checklists/generate
        [HttpPost]
        [Route("generate")]
        [Route("~/api/checklist/generate")]
        public IHttpActionResult GenerateSchedules([FromBody] GenerateSchedulesPayload payload, [FromUri] string fromDate = null, [FromUri] string toDate = null)
        {
            try
            {
                string startStr = payload?.FromDate ?? fromDate;
                string endStr = payload?.ToDate ?? toDate;

                if (string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(endStr))
                {
                    return Ok(new { success = false, message = "Vui lòng chọn đầy đủ khoảng thời gian." });
                }

                DateTime start = DateTime.Parse(startStr, System.Globalization.CultureInfo.InvariantCulture);
                DateTime end = DateTime.Parse(endStr, System.Globalization.CultureInfo.InvariantCulture);

                db.Database.ExecuteSqlCommand(
                    "EXEC sp_GenerateChecklistSchedules @FromDate, @ToDate",
                    new SqlParameter("@FromDate", start),
                    new SqlParameter("@ToDate", end)
                );

                return Ok(new { success = true, message = "Đã sinh lịch kiểm tra thành công cho khoảng thời gian này!" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Lỗi khi sinh lịch kiểm tra: " + ex.Message });
            }
        }

        // GET: api/checklists/device-checklist
        [HttpGet]
        [Route("device-checklist")]
        [Route("~/api/checklist/device-checklist")]
        public IHttpActionResult GetChecklistForDevice(int inventoryId, string cycleType = null)
        {
            try
            {
                var items = db.Database.SqlQuery<ChecklistDefinition>(
                    "EXEC sp_GetChecklistForInventory @InventoryId, @CycleType",
                    new SqlParameter("@InventoryId", inventoryId),
                    new SqlParameter("@CycleType", string.IsNullOrEmpty(cycleType) ? (object)DBNull.Value : cycleType)
                ).ToList();

                // Global items with no CycleType match always apply.
                // Global items with a specific CycleType only apply if no group item overrides that cycle.
                // Group and item scopes always show alongside applicable global items.
                // Filter: only keep global items if their CycleType is NULL (universal) OR there is no group item for that same cycle
                var hasGroupItems = items.Any(i => i.Scope == "group" || i.Scope == "item");
                if (hasGroupItems)
                {
                    items.RemoveAll(i => i.Scope == "global" && !string.IsNullOrEmpty(i.CycleType));
                }

                return Ok(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/checklists/save
        [HttpPost]
        [Route("save")]
        [Route("~/api/checklist/save")]
        public IHttpActionResult SaveChecklist([FromBody] ChecklistLogPayload payload)
        {
            try
            {
                if (payload == null || payload.InventoryId <= 0)
                {
                    return Ok(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        string cycleLower = (payload.CycleType ?? "adhoc").ToLower();
                        var log = new ChecklistLog
                        {
                            ScheduleId = payload.ScheduleId > 0 ? payload.ScheduleId : (int?)null,
                            InventoryId = payload.InventoryId,
                            CheckedBy = (System.Web.HttpContext.Current?.Session?["UserId"] as int?) ?? (payload.CheckedBy > 0 ? payload.CheckedBy : 1),
                            CheckedAt = DateTime.Now,
                            CycleType = payload.CycleType ?? "adhoc",
                            OverallResult = payload.OverallResult ?? "pass",
                            ApprovalStatus = (payload.OverallResult == "pass") ? "Approved" : "Pending",
                            Note = payload.Note,
                            QrLocation = payload.QrLocation,
                            ImageUrls = payload.ImageUrls
                        };

                        if (payload.QrScanned == true)
                        {
                            log.QrScannedAt = DateTime.Now;
                        }

                        db.ChecklistLogs.Add(log);
                        db.SaveChanges();

                        if (payload.Items != null && payload.Items.Any())
                        {
                            foreach (var item in payload.Items)
                            {
                                if (item.IsPassed == false && string.IsNullOrWhiteSpace(item.Note))
                                {
                                    transaction.Rollback();
                                    return Ok(new { success = false, message = "Các hạng mục báo lỗi bắt buộc phải có ghi chú mô tả chi tiết lỗi." });
                                }

                                var logItem = new ChecklistLogItem
                                {
                                    LogId = log.Id,
                                    DefinitionId = item.DefinitionId,
                                    IsPassed = item.IsPassed,
                                    Note = item.Note
                                };
                                db.ChecklistLogItems.Add(logItem);
                            }
                            db.SaveChanges();
                        }

                        if (payload.ScheduleId > 0)
                        {
                            var schedule = db.ChecklistSchedules.Find(payload.ScheduleId);
                            if (schedule != null)
                            {
                                schedule.Status = "done";
                                db.SaveChanges();

                                // Auto-skip older pending/overdue schedules for the same device and cycle
                                var olderSchedules = db.ChecklistSchedules
                                    .Where(s => s.InventoryId == schedule.InventoryId 
                                             && s.CycleType == schedule.CycleType 
                                             && s.ScheduledDate < schedule.ScheduledDate 
                                             && (s.Status == "pending" || s.Status == "overdue"))
                                    .ToList();

                                foreach (var oldSch in olderSchedules)
                                {
                                    oldSch.Status = "skipped";
                                }
                                db.SaveChanges();
                            }
                        }

                        var checkedByUserId = (System.Web.HttpContext.Current?.Session?["UserId"] as int?) ?? (payload.CheckedBy > 0 ? payload.CheckedBy : 1);
                        if (payload.OverallResult == "fail")
                        {
                            var inventory = db.Inventories.Find(payload.InventoryId);
                            if (inventory != null)
                            {
                                inventory.LifeStatus = "suspended";
                                inventory.UpdatedAt = DateTime.Now;
                                inventory.UpdatedBy = checkedByUserId;
                                db.SaveChanges();
                            }
                        }
                        else if (payload.OverallResult == "pass")
                        {
                            var inventory = db.Inventories.Find(payload.InventoryId);
                            if (inventory != null && inventory.LifeStatus == "suspended")
                            {
                                // Reactivation requires both completed repair ticket AND successful re-inspection
                                bool hasCompletedRepair = db.MaintenanceLogs.Any(ml => ml.InventoryId == payload.InventoryId && (ml.Status == "closed" || ml.Status == "completed"));
                                bool hasOpenRepair = db.MaintenanceLogs.Any(ml => ml.InventoryId == payload.InventoryId && (ml.Status == "open" || ml.Status == "in_progress"));
                                
                                if (hasCompletedRepair && !hasOpenRepair)
                                {
                                    inventory.LifeStatus = "active";
                                    inventory.UpdatedAt = DateTime.Now;
                                    inventory.UpdatedBy = checkedByUserId;
                                    db.SaveChanges();
                                }
                            }
                        }

                        transaction.Commit();
                        return Ok(new { success = true, message = "Lưu kết quả checklist thành công!", logId = log.Id });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                var errMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errMsg += " | Inner: " + ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                    {
                        errMsg += " | InnerInner: " + ex.InnerException.InnerException.Message;
                    }
                }
                return Ok(new { success = false, message = "Lỗi khi lưu checklist: " + errMsg });
            }
        }

        // GET: api/checklists/logs
        [HttpGet]
        [Route("logs")]
        [Route("~/api/checklist/logs")]
        public IHttpActionResult GetLogs(string fromDate = null, string toDate = null, string result = null, string approvalStatus = null, int? inventoryId = null)
        {
            try
            {
                var query = db.ChecklistLogs.AsQueryable();

                if (inventoryId.HasValue && inventoryId.Value > 0)
                {
                    query = query.Where(l => l.InventoryId == inventoryId.Value);
                }

                if (!string.IsNullOrEmpty(fromDate))
                {
                    DateTime start = DateTime.Parse(fromDate, System.Globalization.CultureInfo.InvariantCulture);
                    query = query.Where(l => l.CheckedAt >= start);
                }
                if (!string.IsNullOrEmpty(toDate))
                {
                    DateTime end = DateTime.Parse(toDate, System.Globalization.CultureInfo.InvariantCulture).Date.AddDays(1);
                    query = query.Where(l => l.CheckedAt < end);
                }
                if (!string.IsNullOrEmpty(result))
                {
                    query = query.Where(l => l.OverallResult == result);
                }
                if (!string.IsNullOrEmpty(approvalStatus))
                {
                    query = query.Where(l => l.ApprovalStatus == approvalStatus);

                    // Yêu cầu: Không hiển thị "pass" trong màn hình chờ duyệt để đỡ tốn thời gian duyệt
                    if (approvalStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(l => l.OverallResult != "pass" && l.OverallResult != "Pass");
                    }
                }

                var logs = query
                    .OrderByDescending(l => l.CheckedAt)
                    .Select(l => new
                    {
                        l.Id,
                        l.ScheduleId,
                        l.InventoryId,
                        AssetCode = l.Inventory != null ? l.Inventory.AssetCode : "",
                        ItemName = (l.Inventory != null && l.Inventory.Item != null) ? l.Inventory.Item.Name : "N/A",
                        SerialNumber = l.Inventory != null ? l.Inventory.SerialNumber : "",
                        DepartmentName = (l.Inventory != null && l.Inventory.Department != null) ? l.Inventory.Department.Name : "",
                        CheckedAt = l.CheckedAt,
                        CheckedByName = l.CheckedByUser != null ? l.CheckedByUser.FullName : "Admin",
                        l.CycleType,
                        l.OverallResult,
                        l.ApprovalStatus,
                        l.Note,
                        GroupId = (l.Inventory != null && l.Inventory.Item != null && l.Inventory.Item.Group != null) 
                            ? l.Inventory.Item.Group.Id : 0,
                        GroupName = (l.Inventory != null && l.Inventory.Item != null && l.Inventory.Item.Group != null) 
                            ? l.Inventory.Item.Group.Name : "Chưa phân nhóm",
                        GroupIcon = (l.Inventory != null && l.Inventory.Item != null && l.Inventory.Item.Group != null) 
                            ? l.Inventory.Item.Group.Icon : "📦",
                    })
                    .ToList()
                    .Select(l => new
                    {
                        l.Id,
                        l.ScheduleId,
                        l.InventoryId,
                        l.AssetCode,
                        l.ItemName,
                        l.SerialNumber,
                        l.DepartmentName,
                        CheckedAt = l.CheckedAt.ToString("yyyy-MM-dd HH:mm"),
                        l.CheckedByName,
                        l.CycleType,
                        l.OverallResult,
                        l.ApprovalStatus,
                        l.Note,
                        l.GroupId,
                        l.GroupName,
                        l.GroupIcon
                    })
                    .ToList();

                return Ok(new { success = true, data = logs });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/checklists/log-details
        [HttpGet]
        [Route("log-details")]
        [Route("~/api/checklist/log-details")]
        public IHttpActionResult GetLogDetails(int logId)
        {
            try
            {
                var log = db.ChecklistLogs.Find(logId);
                if (log == null)
                {
                    return Ok(new { success = false, message = "Không tìm thấy nhật ký kiểm tra." });
                }

                var details = db.ChecklistLogItems
                    .Where(i => i.LogId == logId)
                    .Select(i => new
                    {
                        i.Id,
                        i.DefinitionId,
                        CheckName = i.Definition != null ? i.Definition.CheckName : "N/A",
                        i.IsPassed,
                        i.Note
                    })
                    .ToList();

                var logDto = new
                {
                    log.Id,
                    AssetCode = log.Inventory != null ? log.Inventory.AssetCode : "",
                    ItemName = (log.Inventory != null && log.Inventory.Item != null) ? log.Inventory.Item.Name : "N/A",
                    SerialNumber = log.Inventory != null ? log.Inventory.SerialNumber : "",
                    DepartmentName = (log.Inventory != null && log.Inventory.Department != null) ? log.Inventory.Department.Name : "",
                    CheckedAt = log.CheckedAt.ToString("yyyy-MM-dd HH:mm"),
                    CheckedByName = log.CheckedByUser != null ? log.CheckedByUser.FullName : "Admin",
                    log.CycleType,
                    log.OverallResult,
                    log.ApprovalStatus,
                    log.Note,
                    log.QrScannedAt,
                    log.QrLocation,
                    log.ImageUrls,
                    Items = details
                };

                return Ok(new { success = true, data = logDto });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/checklists/department-progress
        [HttpGet]
        [Route("department-progress")]
        [Route("~/api/checklist/department-progress")]
        public IHttpActionResult GetDepartmentProgress(string fromDate = null, string toDate = null)
        {
            try
            {
                DateTime start = DateTime.Today.AddDays(-30);
                DateTime end = DateTime.Today;

                if (!string.IsNullOrEmpty(fromDate))
                {
                    start = DateTime.Parse(fromDate, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (!string.IsNullOrEmpty(toDate))
                {
                    end = DateTime.Parse(toDate, System.Globalization.CultureInfo.InvariantCulture);
                }

                DateTime startRange = start.Date;
                DateTime endRange = end.Date.AddDays(1);

                // Lấy tất cả phòng ban
                var departments = db.Departments.ToList();

                // Group stats from logs
                var doneStats = db.ChecklistLogs
                    .Where(l => l.CheckedAt >= startRange && l.CheckedAt < endRange)
                    .GroupBy(l => l.Inventory.DepartmentId)
                    .Select(g => new { 
                        DepartmentId = g.Key, 
                        DoneCount = g.Count(),
                        PassCount = g.Count(x => x.OverallResult == "pass"),
                        FailCount = g.Count(x => x.OverallResult != "pass")
                    })
                    .ToDictionary(g => g.DepartmentId ?? 0, g => g);

                // Count pending schedules
                var pendingCounts = db.ChecklistSchedules
                    .Where(s => s.ScheduledDate >= startRange && s.ScheduledDate < endRange && s.Status == "pending")
                    .GroupBy(s => s.Inventory.DepartmentId)
                    .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
                    .ToDictionary(g => g.DepartmentId ?? 0, g => g.Count);

                // Count overdue schedules
                var overdueCounts = db.ChecklistSchedules
                    .Where(s => s.ScheduledDate < startRange && s.Status == "pending")
                    .GroupBy(s => s.Inventory.DepartmentId)
                    .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
                    .ToDictionary(g => g.DepartmentId ?? 0, g => g.Count);

                var progressData = departments.Select(d =>
                {
                    var stat = doneStats.ContainsKey(d.Id) ? doneStats[d.Id] : null;
                    int done = stat != null ? stat.DoneCount : 0;
                    int passed = stat != null ? stat.PassCount : 0;
                    int failed = stat != null ? stat.FailCount : 0;
                    
                    int pending = pendingCounts.ContainsKey(d.Id) ? pendingCounts[d.Id] : 0;
                    int overdue = overdueCounts.ContainsKey(d.Id) ? overdueCounts[d.Id] : 0;
                    int total = done + pending + overdue;
                    
                    int completionRate = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;
                    int passRate = done > 0 ? (int)Math.Round((double)passed / done * 100) : 0;
                    int failRate = done > 0 ? (int)Math.Round((double)failed / done * 100) : 0;

                    return new
                    {
                        DepartmentId = d.Id,
                        DepartmentName = d.Name,
                        DoneCount = done,
                        PendingCount = pending,
                        OverdueCount = overdue,
                        TotalCount = total,
                        CompletionRate = completionRate,
                        PassRate = passRate,
                        FailRate = failRate
                    };
                })
                .OrderByDescending(p => p.TotalCount)
                .ToList();

                return Ok(new { success = true, data = progressData });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class ReviewMultiplePayload
        {
            public List<int> logIds { get; set; }
            public string status { get; set; } // 'Approved' | 'Rejected'
        }

        // POST: api/checklists/approve-multiple
        [HttpPost]
        [Route("approve-multiple")]
        [Route("~/api/checklist/approve-multiple")]
        public IHttpActionResult ApproveMultiple([FromBody] ReviewMultiplePayload payload)
        {
            try
            {
                if (payload == null || payload.logIds == null || !payload.logIds.Any())
                {
                    return Ok(new { success = false, message = "Vui lòng chọn ít nhất một nhật ký để duyệt/từ chối." });
                }

                string newStatus = string.IsNullOrEmpty(payload.status) ? "Approved" : payload.status;
                if (newStatus != "Approved" && newStatus != "Rejected") 
                    newStatus = "Approved";

                var userId = (System.Web.HttpContext.Current?.Session?["UserId"] as int?) ?? 1;

                var logs = db.ChecklistLogs.Where(l => payload.logIds.Contains(l.Id) && l.ApprovalStatus == "Pending").ToList();
                foreach (var log in logs)
                {
                    log.ApprovalStatus = newStatus;
                    
                    // If rejected, set schedule back to NeedsReinspection
                    if (newStatus == "Rejected" && log.ScheduleId.HasValue)
                    {
                        var schedule = db.ChecklistSchedules.Find(log.ScheduleId.Value);
                        if (schedule != null)
                        {
                            schedule.Status = "NeedsReinspection";
                        }
                    }
                }
                db.SaveChanges();

                return Ok(new { success = true, message = $"Đã { (newStatus == "Approved" ? "duyệt" : "từ chối") } thành công {logs.Count} nhật ký checklist!" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Lỗi khi xử lý hàng loạt: " + ex.Message });
            }
        }

        // GET: api/checklists/operational-kpis
        [HttpGet]
        [Route("operational-kpis")]
        [Route("~/api/checklist/operational-kpis")]
        public IHttpActionResult GetOperationalKPIs()
        {
            try
            {
                // All time or limited time, for now let's do all time or current month
                DateTime startRange = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                
                var totalSchedules = db.ChecklistSchedules.Count();
                var completedSchedules = db.ChecklistLogs.Count();
                var pendingSchedules = db.ChecklistSchedules.Count(s => s.Status == "pending" || s.Status == "NeedsReinspection");
                var overdueSchedules = db.ChecklistSchedules.Count(s => s.ScheduledDate < DateTime.Today && (s.Status == "pending" || s.Status == "NeedsReinspection"));

                var passedLogs = db.ChecklistLogs.Count(l => l.OverallResult == "pass");
                var failedLogs = db.ChecklistLogs.Count(l => l.OverallResult != "pass");
                var totalReviewed = passedLogs + failedLogs;

                int completionRate = totalSchedules > 0 ? (int)Math.Round((double)completedSchedules / totalSchedules * 100) : 0;
                int passRate = totalReviewed > 0 ? (int)Math.Round((double)passedLogs / totalReviewed * 100) : 0;
                int failRate = totalReviewed > 0 ? (int)Math.Round((double)failedLogs / totalReviewed * 100) : 0;

                var totalInspectedAssets = db.ChecklistLogs.Select(l => l.InventoryId).Distinct().Count();
                var passedAssets = db.ChecklistLogs.Where(l => l.OverallResult == "pass").Select(l => l.InventoryId).Distinct().Count();
                int complianceRate = totalInspectedAssets > 0 ? (int)Math.Round((double)passedAssets / totalInspectedAssets * 100) : 0;

                // Mocking tickets for now since we may not have a clear link
                var repairTicketsCreated = 0;
                var repairTicketsCompleted = 0;

                var suspendedAssets = db.Inventories.Count(i => i.LifeStatus == "suspended");

                return Ok(new { 
                    success = true, 
                    data = new {
                        TotalScheduled = totalSchedules,
                        TotalCompleted = completedSchedules,
                        TotalPending = pendingSchedules,
                        Overdue = overdueSchedules,
                        CompletionRate = completionRate,
                        PassRate = passRate,
                        FailRate = failRate,
                        ComplianceRate = complianceRate,
                        FailedChecklists = failedLogs,
                        RepairTicketsCreated = repairTicketsCreated,
                        RepairTicketsCompleted = repairTicketsCompleted,
                        SuspendedAssets = suspendedAssets
                    } 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // GET: api/checklists/active-groups
        [HttpGet]
        [Route("active-groups")]
        public IHttpActionResult GetActiveGroups()
        {
            try
            {
                var groups = db.Groups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.SortOrder)
                    .Select(g => new
                    {
                        g.Id,
                        g.Code,
                        g.Name,
                        g.Icon
                    })
                    .ToList();
                return Ok(new { success = true, data = groups });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/checklists/group-definitions
        [HttpGet]
        [Route("group-definitions")]
        public IHttpActionResult GetGroupDefinitions(int groupId, string cycleType = null)
        {
            try
            {
                var query = db.ChecklistDefinitions.Where(d => d.IsActive);

                query = query.Where(d => 
                    (d.Scope == "global" && (string.IsNullOrEmpty(cycleType) || d.CycleType == null || d.CycleType == cycleType)) ||
                    (d.Scope == "group" && d.GroupId == groupId && (string.IsNullOrEmpty(cycleType) || d.CycleType == null || d.CycleType == cycleType))
                );

                var list = query
                    .OrderBy(d => d.Scope == "global" ? 0 : 1)
                    .ThenBy(d => d.SortOrder)
                    .ToList();

                // Keep universal global items (CycleType IS NULL), remove cycle-specific global if group exists
                var hasGroupItems = list.Any(i => i.Scope == "group" || i.Scope == "item");
                if (hasGroupItems)
                {
                    list.RemoveAll(i => i.Scope == "global" && !string.IsNullOrEmpty(i.CycleType));
                }

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/checklists/batch-check-group
        [HttpPost]
        [Route("batch-check-group")]
        public IHttpActionResult BatchCheckGroup([FromBody] BatchCheckPayload payload)
        {
            if (payload == null || payload.ScheduleIds == null || !payload.ScheduleIds.Any())
            {
                return Ok(new { success = false, message = "Vui lòng chọn ít nhất một lịch trình." });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var userId = (System.Web.HttpContext.Current?.Session?["UserId"] as int?) 
                                 ?? (payload.CheckedBy > 0 ? payload.CheckedBy : 1);

                    // 1. Lọc chỉ lấy schedules CÒN pending/NeedsReinspection (chống trùng lặp concurrent)
                    // Note: "overdue" is computed client-side (pending + DueDate < Today), NOT stored in DB
                    var schedules = db.ChecklistSchedules
                        .Include(s => s.Inventory)
                        .Include(s => s.Inventory.Item)
                        .Include(s => s.Inventory.Item.Group)
                        .Where(s => payload.ScheduleIds.Contains(s.Id) 
                                 && (s.Status == "pending" || s.Status == "NeedsReinspection"))
                        .ToList();

                    if (schedules.Count == 0)
                    {
                        return Ok(new { success = false, 
                            message = "Tất cả lịch trình đã được check bởi người khác hoặc không hợp lệ." });
                    }

                    // 1b. Validation: Ensure all schedules belong to the same GroupId and CycleType
                    var firstSch = schedules.First();
                    var firstGroupId = (firstSch.Inventory?.Item != null) ? firstSch.Inventory.Item.GroupId : (int?)null;
                    var firstCycleType = firstSch.CycleType;

                    foreach(var sch in schedules)
                    {
                        var groupId = (sch.Inventory?.Item != null) ? sch.Inventory.Item.GroupId : (int?)null;
                        if (groupId != firstGroupId)
                            return Ok(new { success = false, message = "Không được mix nhiều nhóm tài sản (Asset Groups) trong cùng một lượt check hàng loạt." });
                        if (sch.CycleType != firstCycleType)
                            return Ok(new { success = false, message = "Không được mix nhiều chu kỳ kiểm tra (Cycle Type) trong cùng một lượt check hàng loạt." });
                        // Department validation removed: batch groups by GroupId+CycleType, not department

                        if (sch.Inventory != null)
                        {
                            var crit = string.IsNullOrEmpty(sch.Inventory.Criticality) ? "Low" : sch.Inventory.Criticality;
                            if (crit == "High" || crit == "Critical")
                            {
                                return Ok(new { success = false, message = $"Thiết bị {sch.Inventory.AssetCode} có độ quan trọng cao ({crit}), bắt buộc phải kiểm tra riêng lẻ." });
                            }
                            if (sch.Inventory.LifeStatus != "active")
                            {
                                return Ok(new { success = false, message = $"Thiết bị {sch.Inventory.AssetCode} không ở trạng thái Hoạt động (Active), không thể kiểm tra hàng loạt." });
                            }
                        }
                    }

                    int skippedCount = payload.ScheduleIds.Count - schedules.Count;

                    // 2. Tạo logs + items bằng AddRange (tối ưu SQL batch INSERT)
                    var allLogs = new List<ChecklistLog>();

                    foreach (var sch in schedules)
                    {
                        string cycleLower = (sch.CycleType ?? "adhoc").ToLower();
                        string result = payload.Mode == "quick" ? "pass" : payload.OverallResult;

                        var log = new ChecklistLog
                        {
                            ScheduleId = sch.Id,
                            InventoryId = sch.InventoryId,
                            CheckedBy = userId,
                            CheckedAt = DateTime.Now,
                            CycleType = sch.CycleType,
                            OverallResult = result,
                            ApprovalStatus = (result == "pass") ? "Approved" : "Pending",
                            Note = payload.Note
                        };
                        allLogs.Add(log);
                        sch.Status = "done";  // Cập nhật schedule
                    }

                    // AddRange cho logs
                    db.ChecklistLogs.AddRange(allLogs);
                    db.SaveChanges();  // Flush để có Log.Id

                    // Auto-skip older pending/overdue schedules for each completed device
                    foreach (var sch in schedules)
                    {
                        var olderSchedules = db.ChecklistSchedules
                            .Where(s => s.InventoryId == sch.InventoryId 
                                     && s.CycleType == sch.CycleType 
                                     && s.ScheduledDate < sch.ScheduledDate 
                                     && (s.Status == "pending" || s.Status == "overdue"))
                            .ToList();
                        foreach (var oldSch in olderSchedules)
                        {
                            oldSch.Status = "skipped";
                        }
                    }
                    db.SaveChanges();

                    // 3. Tạo log items (nếu có)
                    var allLogItems = new List<ChecklistLogItem>();
                    if (payload.Items != null && payload.Items.Any())
                    {
                        foreach (var log in allLogs)
                        {
                            foreach (var item in payload.Items)
                            {
                                allLogItems.Add(new ChecklistLogItem
                                {
                                    LogId = log.Id,
                                    DefinitionId = item.DefinitionId,
                                    IsPassed = payload.Mode == "quick" ? true : item.IsPassed,
                                    Note = payload.Mode == "quick" ? null : item.Note
                                });
                            }
                        }
                        db.ChecklistLogItems.AddRange(allLogItems);
                        db.SaveChanges();
                    }

                    // 4. Nếu có thiết bị có OverallResult == "fail" -> đổi trạng thái thành "suspended" (tạm ngưng)
                    if (payload.Mode != "quick" && payload.OverallResult == "fail")
                    {
                        var inventoryIds = schedules.Select(s => s.InventoryId).Distinct().ToList();
                        var inventories = db.Inventories.Where(i => inventoryIds.Contains(i.Id)).ToList();
                        foreach (var inv in inventories)
                        {
                            inv.LifeStatus = "suspended";
                            inv.UpdatedAt = DateTime.Now;
                            inv.UpdatedBy = userId;
                        }
                        db.SaveChanges();
                    }

                    transaction.Commit();

                    string msg = $"Đã check thành công {schedules.Count} thiết bị.";
                    if (skippedCount > 0)
                        msg += $" ({skippedCount} thiết bị đã được check trước đó, bỏ qua.)";

                    int? firstLogId = allLogs.FirstOrDefault()?.Id;
                    int? failedLogId = allLogs.FirstOrDefault(l => l.OverallResult != "pass")?.Id;

                    return Ok(new { 
                        success = true, 
                        message = msg, 
                        checkedCount = schedules.Count, 
                        skippedCount = skippedCount,
                        firstLogId = firstLogId,
                        failedLogId = failedLogId
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Ok(new { success = false, message = "Lỗi batch check: " + ex.Message });
                }
            }
        }
    }

    public class BatchCheckPayload
    {
        public List<int> ScheduleIds { get; set; }
        public string Mode { get; set; } // "quick" | "template"
        public string OverallResult { get; set; } // "pass" | "fail" | "partial"
        public List<ChecklistLogItemPayload> Items { get; set; }
        public string Note { get; set; }
        public int CheckedBy { get; set; }
    }

    public class GenerateSchedulesPayload
    {
        public string FromDate { get; set; }
        public string ToDate { get; set; }
    }

    public class ChecklistLogPayload
    {
        public int ScheduleId { get; set; }
        public int InventoryId { get; set; }
        public int CheckedBy { get; set; }
        public string CycleType { get; set; }
        public string OverallResult { get; set; }
        public string Note { get; set; }
        public bool? QrScanned { get; set; }
        public string QrLocation { get; set; }
        public string ImageUrls { get; set; }
        public List<ChecklistLogItemPayload> Items { get; set; }
    }

    public class ChecklistLogItemPayload
    {
        public int DefinitionId { get; set; }
        public bool IsPassed { get; set; }
        public string Note { get; set; }
    }
}
