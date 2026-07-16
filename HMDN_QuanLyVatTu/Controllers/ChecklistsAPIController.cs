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
                // Lazy schedule generation for the current month if not yet generated
                try
                {
                    new HMDN_QuanLyVatTu.Services.ChecklistSchedulerService().EnsureSchedulesGeneratedForCurrentMonth(db);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"[ChecklistAPI] Lazy schedule generation failed: {ex.Message}");
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
                        s.LocationId,
                        LocationName = s.Location != null ? s.Location.Name : "",
                        LocationCode = s.Location != null ? s.Location.Code : "",
                        AssetCode = s.Inventory != null ? s.Inventory.AssetCode : (s.Location != null ? s.Location.Code : ""),
                        ItemName = (s.Inventory != null && s.Inventory.Item != null) ? s.Inventory.Item.Name : (s.Location != null ? s.Location.Name : "N/A"),
                        SerialNumber = s.Inventory != null ? s.Inventory.SerialNumber : "",
                        DepartmentName = (s.Inventory != null && s.Inventory.Department != null) 
                            ? s.Inventory.Department.Name 
                            : (s.Location != null && s.Location.DepartmentId != null 
                                ? db.Departments.Where(d => d.Id == s.Location.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? "" 
                                : ""),
                        QrCode = s.Inventory != null ? s.Inventory.QrCode : (s.Location != null ? s.Location.Code : ""),
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
                            ? s.Inventory.Item.Group.Name : "Khu vực / Phòng máy",
                        GroupIcon = (s.Inventory != null && s.Inventory.Item != null && s.Inventory.Item.Group != null) 
                            ? s.Inventory.Item.Group.Icon : "🏢",
                        LifeStatus = s.Inventory != null ? s.Inventory.LifeStatus : "active",
                        Criticality = s.Inventory != null ? s.Inventory.Criticality : "Low"
                    })
                    .ToList()
                    .Select(s => new
                    {
                        s.Id,
                        s.InventoryId,
                        s.LocationId,
                        s.LocationName,
                        s.LocationCode,
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
                        HasOpenRepair = s.InventoryId.HasValue ? openRepairInventoryIds.Contains(s.InventoryId.Value) : false
                    })
                    .ToList();

                var latestPendingDict = resolvedSchedules
                    .Where(s => (s.Status == "pending" || s.Status == "overdue" || s.Status == "NeedsReinspection") && s.ScheduledDate <= DateTime.Today)
                    .GroupBy(s => new { s.InventoryId, s.LocationId, s.CycleType })
                    .ToDictionary(
                        g => new { g.Key.InventoryId, g.Key.LocationId, g.Key.CycleType },
                        g => g.OrderByDescending(s => s.ScheduledDate).First().Id
                    );

                var schedules = resolvedSchedules
                    .Where(s => 
                        (s.Status != "pending" && s.Status != "overdue" && s.Status != "NeedsReinspection")
                        || s.ScheduledDate > DateTime.Today
                        || (latestPendingDict.TryGetValue(new { s.InventoryId, s.LocationId, s.CycleType }, out int latestId) && latestId == s.Id)
                    )
                    .Select(s => new
                    {
                        s.Id,
                        s.InventoryId,
                        s.LocationId,
                        s.LocationName,
                        s.LocationCode,
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

                var resolver = new HMDN_QuanLyVatTu.Services.ChecklistDefinitionResolver();
                var resolvedItems = resolver.ResolveApplicableDefinitions(items);

                return Ok(new { success = true, data = resolvedItems });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/checklists/template
        [HttpGet]
        [Route("template")]
        [Route("~/api/checklist/template")]
        public IHttpActionResult GetChecklistTemplate(int scope, int targetId, string cycleType = null, string scheduledDate = null)
        {
            try
            {
                int? resolvedTemplateVersionId = null;
                DateTime? sDate = null;
                if (!string.IsNullOrEmpty(scheduledDate) && DateTime.TryParse(scheduledDate, out DateTime tempDate))
                {
                    sDate = tempDate;
                }

                // 1. Resolve template version using mapping
                var mapping = db.ChecklistTemplateMappings
                    .Where(m => m.Scope == scope && m.TargetId == targetId && m.IsActive)
                    .Where(m => string.IsNullOrEmpty(cycleType) || m.CycleType == cycleType)
                    .OrderByDescending(m => m.Id)
                    .FirstOrDefault();

                if (mapping != null)
                {
                    resolvedTemplateVersionId = mapping.TemplateVersionId;
                }
                else if (scope == 3) // Scope.Asset
                {
                    var inventory = db.Inventories.Find(targetId);
                    if (inventory != null)
                    {
                        var groupId = inventory.Item?.GroupId;
                        if (groupId.HasValue)
                        {
                            var catMapping = db.ChecklistTemplateMappings
                                .Where(m => m.Scope == 2 && m.TargetId == groupId.Value && m.IsActive)
                                .Where(m => string.IsNullOrEmpty(cycleType) || m.CycleType == cycleType)
                                .OrderByDescending(m => m.Id)
                                .FirstOrDefault();
                            if (catMapping != null)
                            {
                                resolvedTemplateVersionId = catMapping.TemplateVersionId;
                            }
                        }

                        if (resolvedTemplateVersionId == null)
                        {
                            var globalMapping = db.ChecklistTemplateMappings
                                .Where(m => m.Scope == 1 && m.IsActive)
                                .Where(m => string.IsNullOrEmpty(cycleType) || m.CycleType == cycleType)
                                .OrderByDescending(m => m.Id)
                                .FirstOrDefault();
                            if (globalMapping != null)
                            {
                                resolvedTemplateVersionId = globalMapping.TemplateVersionId;
                            }
                        }
                    }
                }

                if (resolvedTemplateVersionId.HasValue)
                {
                    var version = db.ChecklistTemplateVersions
                        .Include(v => v.Template)
                        .Include(v => v.Definitions)
                        .FirstOrDefault(v => v.Id == resolvedTemplateVersionId.Value);

                    if (version != null)
                    {
                        var defsQuery = version.Definitions.Where(d => d.IsActive);

                        if (!string.IsNullOrEmpty(cycleType))
                        {
                            var cycle = cycleType.ToLower();
                            if (cycle == "daily")
                            {
                                // Đối với ca kiểm tra hàng ngày, chỉ đưa vào hạng mục hàng tuần nếu là thứ Hai, hàng tháng nếu là ngày 1
                                defsQuery = defsQuery.Where(d =>
                                    string.IsNullOrEmpty(d.CycleType) ||
                                    d.CycleType.ToLower() == "daily" ||
                                    (d.CycleType.ToLower() == "weekly" && sDate.HasValue && sDate.Value.DayOfWeek == DayOfWeek.Monday) ||
                                    (d.CycleType.ToLower() == "monthly" && sDate.HasValue && sDate.Value.Day == 1) ||
                                    (d.CycleType.ToLower() == "yearly" && sDate.HasValue && sDate.Value.Day == 1 && sDate.Value.Month == 1)
                                );
                            }
                            else if (cycle == "weekly")
                            {
                                defsQuery = defsQuery.Where(d =>
                                    string.IsNullOrEmpty(d.CycleType) ||
                                    d.CycleType.ToLower() == "daily" ||
                                    d.CycleType.ToLower() == "weekly"
                                );
                            }
                            else if (cycle == "monthly")
                            {
                                defsQuery = defsQuery.Where(d =>
                                    string.IsNullOrEmpty(d.CycleType) ||
                                    d.CycleType.ToLower() == "daily" ||
                                    d.CycleType.ToLower() == "weekly" ||
                                    d.CycleType.ToLower() == "monthly"
                                );
                            }
                        }

                        var items = defsQuery
                            .OrderBy(d => d.SortOrder)
                            .ThenBy(d => d.Id)
                            .Select(d => new
                            {
                                d.Id,
                                d.CheckName,
                                d.Description,
                                d.ValueType,
                                d.Unit,
                                d.ValidationRules,
                                d.Severity,
                                d.IsRequired,
                                d.SortOrder,
                                Options = db.ChecklistDefinitionOptions
                                    .Where(o => o.ChecklistDefinitionId == d.Id && o.IsActive)
                                    .OrderBy(o => o.SortOrder)
                                    .Select(o => new
                                    {
                                        o.Value,
                                        o.DisplayText,
                                        o.Color,
                                        o.IsDefault
                                    }).ToList()
                            }).ToList();

                        return Ok(new
                        {
                            success = true,
                            templateId = version.TemplateId,
                            templateVersionId = version.Id,
                            templateName = version.Template.Name,
                            versionNumber = version.Version,
                            data = items
                        });
                    }
                }

                // 2. Backward Compatibility Fallback
                if (scope == 3)
                {
                    var legacyDefs = db.Database.SqlQuery<ChecklistDefinition>(
                        "EXEC sp_GetChecklistForInventory @InventoryId, @CycleType, @ScheduledDate",
                        new SqlParameter("@InventoryId", targetId),
                        new SqlParameter("@CycleType", string.IsNullOrEmpty(cycleType) ? (object)DBNull.Value : cycleType),
                        new SqlParameter("@ScheduledDate", sDate.HasValue ? (object)sDate.Value : DBNull.Value)
                    ).ToList();

                    var resolver = new HMDN_QuanLyVatTu.Services.ChecklistDefinitionResolver();
                    var resolvedItems = resolver.ResolveApplicableDefinitions(legacyDefs);

                    var items = resolvedItems.Select(d => new
                    {
                        d.Id,
                        d.CheckName,
                        d.Description,
                        ValueType = d.ValueType ?? "checkbox",
                        Unit = d.Unit,
                        ValidationRules = d.ValidationRules,
                        Severity = d.Severity ?? "Information",
                        d.IsRequired,
                        d.SortOrder,
                        Options = db.ChecklistDefinitionOptions
                            .Where(o => o.ChecklistDefinitionId == d.Id && o.IsActive)
                            .OrderBy(o => o.SortOrder)
                            .Select(o => new
                            {
                                o.Value,
                                o.DisplayText,
                                o.Color,
                                o.IsDefault
                            }).ToList()
                    }).ToList();

                    return Ok(new
                    {
                        success = true,
                        templateId = (int?)null,
                        templateVersionId = (int?)null,
                        templateName = "Legacy Asset Checklist",
                        versionNumber = 1,
                        data = items
                    });
                }

                return Ok(new { success = false, message = "Không tìm thấy biểu mẫu checklist phù hợp cho phạm vi này." });
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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage + " " + e.Exception?.Message).ToList();
                return Content(System.Net.HttpStatusCode.BadRequest, new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join("; ", errors) });
            }

            try
            {
                if (payload == null)
                {
                    return Ok(new { success = false, message = "Dữ liệu gửi lên trống (payload is null)." });
                }

                if (!payload.InventoryId.HasValue && !payload.LocationId.HasValue)
                {
                    return Ok(new { success = false, message = "Dữ liệu không hợp lệ: Cần liên kết với thiết bị (InventoryId) hoặc vị trí (LocationId)." });
                }

                if (payload.Items == null || !payload.Items.Any())
                {
                    return Ok(new { success = false, message = "Không thể lưu nhật ký checklist rỗng (cần ít nhất một hạng mục kiểm tra)." });
                }

                foreach (var item in payload.Items)
                {
                    if (item.DefinitionId <= 0)
                    {
                        return Ok(new { success = false, message = "Dữ liệu không hợp lệ: Có hạng mục kiểm tra bị thiếu mã định nghĩa (DefinitionId)." });
                    }
                }

                if (payload.ScheduleId > 0)
                {
                    var sch = db.ChecklistSchedules.Find(payload.ScheduleId);
                    if (sch != null && sch.ScheduledDate > DateTime.Today)
                    {
                        return BadRequest("Future checklist schedules cannot be completed.");
                    }
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        string cycleLower = (payload.CycleType ?? "adhoc").ToLower();
                        var log = new ChecklistLog
                        {
                            ScheduleId = payload.ScheduleId > 0 ? payload.ScheduleId : (int?)null,
                            InventoryId = payload.InventoryId > 0 ? payload.InventoryId : (int?)null,
                            LocationId = payload.LocationId > 0 ? payload.LocationId : (int?)null,
                            TemplateVersionId = payload.TemplateVersionId > 0 ? payload.TemplateVersionId : (int?)null,
                            CheckedBy = (System.Web.HttpContext.Current?.Session?["UserId"] as int?) ?? (payload.CheckedBy > 0 ? payload.CheckedBy : 1),
                            CheckedAt = DateTime.Now,
                            CycleType = payload.CycleType ?? "adhoc",
                            OverallResult = payload.OverallResult ?? "pass",
                            ApprovalStatus = "Pending",
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
                                    NumericValue = item.NumericValue,
                                    StringValue = item.StringValue,
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
                            }
                        }

                        var checkedByUserId = log.CheckedBy;
                        if (payload.OverallResult == "fail" && payload.InventoryId > 0)
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
                        else if (payload.OverallResult == "pass" && payload.InventoryId > 0)
                        {
                            var inventory = db.Inventories.Find(payload.InventoryId);
                            if (inventory != null && inventory.LifeStatus == "suspended")
                            {
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
                var curr = ex.InnerException;
                while (curr != null)
                {
                    errMsg += " | Inner: " + curr.Message;
                    curr = curr.InnerException;
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
                }

                var logs = query
                    .OrderByDescending(l => l.CheckedAt)
                    .Select(l => new
                    {
                        l.Id,
                        l.ScheduleId,
                        l.InventoryId,
                        AssetCode = l.Inventory != null ? l.Inventory.AssetCode : (l.Location != null ? l.Location.Code : ""),
                        ItemName = (l.Inventory != null && l.Inventory.Item != null) ? l.Inventory.Item.Name : (l.Location != null ? l.Location.Name : "N/A"),
                        SerialNumber = l.Inventory != null ? l.Inventory.SerialNumber : "",
                        DepartmentName = (l.Inventory != null && l.Inventory.Department != null) 
                            ? l.Inventory.Department.Name 
                            : (l.Location != null && l.Location.DepartmentId != null 
                                ? db.Departments.Where(d => d.Id == l.Location.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? "" 
                                : ""),
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
                    AssetCode = log.Inventory != null ? log.Inventory.AssetCode : (log.Location != null ? log.Location.Code : ""),
                    ItemName = (log.Inventory != null && log.Inventory.Item != null) ? log.Inventory.Item.Name : (log.Location != null ? log.Location.Name : "N/A"),
                    SerialNumber = log.Inventory != null ? log.Inventory.SerialNumber : "",
                    DepartmentName = (log.Inventory != null && log.Inventory.Department != null) 
                        ? log.Inventory.Department.Name 
                        : (log.Location != null && log.Location.DepartmentId != null 
                            ? db.Departments.Where(d => d.Id == log.Location.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? "" 
                            : ""),
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
                    .GroupBy(l => l.Inventory != null ? l.Inventory.DepartmentId : (l.Location != null ? l.Location.DepartmentId : null))
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
                    .GroupBy(s => s.Inventory != null ? s.Inventory.DepartmentId : (s.Location != null ? s.Location.DepartmentId : null))
                    .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
                    .ToDictionary(g => g.DepartmentId ?? 0, g => g.Count);

                // Count overdue schedules
                var overdueCounts = db.ChecklistSchedules
                    .Where(s => s.ScheduledDate < startRange && s.Status == "pending")
                    .GroupBy(s => s.Inventory != null ? s.Inventory.DepartmentId : (s.Location != null ? s.Location.DepartmentId : null))
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

                    if (schedules.Any(s => s.ScheduledDate > DateTime.Today))
                    {
                        return BadRequest("Future checklist schedules cannot be completed.");
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
        public int? ScheduleId { get; set; }
        public int? InventoryId { get; set; }
        public int? LocationId { get; set; }
        public int? TemplateVersionId { get; set; }
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
        public decimal? NumericValue { get; set; }
        public string StringValue { get; set; }
        public string Note { get; set; }
    }
}
