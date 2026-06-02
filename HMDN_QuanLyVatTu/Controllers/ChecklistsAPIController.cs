using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;
using HMS.Data;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/checklists")]
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
                var query = db.ChecklistSchedules.AsQueryable();

                if (!string.IsNullOrEmpty(fromDate))
                {
                    DateTime start = DateTime.Parse(fromDate);
                    query = query.Where(s => s.ScheduledDate >= start);
                }
                if (!string.IsNullOrEmpty(toDate))
                {
                    DateTime end = DateTime.Parse(toDate);
                    query = query.Where(s => s.ScheduledDate <= end);
                }
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(s => s.Status == status);
                }
                if (!string.IsNullOrEmpty(cycleType))
                {
                    query = query.Where(s => s.CycleType == cycleType);
                }

                var schedules = query
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
                        AssigneeName = s.Assignee != null ? s.Assignee.FullName : ""
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
                        ScheduledDate = s.ScheduledDate.ToString("yyyy-MM-dd"),
                        s.CycleType,
                        s.Status,
                        DueDate = s.DueDate.ToString("yyyy-MM-dd"),
                        s.AssignedTo,
                        s.AssigneeName
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

                DateTime start = DateTime.Parse(startStr);
                DateTime end = DateTime.Parse(endStr);

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
                        var log = new ChecklistLog
                        {
                            ScheduleId = payload.ScheduleId > 0 ? payload.ScheduleId : (int?)null,
                            InventoryId = payload.InventoryId,
                            CheckedBy = payload.CheckedBy > 0 ? payload.CheckedBy : 1,
                            CheckedAt = DateTime.Now,
                            CycleType = payload.CycleType ?? "adhoc",
                            OverallResult = payload.OverallResult ?? "pass",
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
        public IHttpActionResult GetLogs(string fromDate = null, string toDate = null, string result = null)
        {
            try
            {
                var query = db.ChecklistLogs.AsQueryable();

                if (!string.IsNullOrEmpty(fromDate))
                {
                    DateTime start = DateTime.Parse(fromDate);
                    query = query.Where(l => l.CheckedAt >= start);
                }
                if (!string.IsNullOrEmpty(toDate))
                {
                    DateTime end = DateTime.Parse(toDate);
                    query = query.Where(l => l.CheckedAt <= end);
                }
                if (!string.IsNullOrEmpty(result))
                {
                    query = query.Where(l => l.OverallResult == result);
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
                        l.Note
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
                        l.Note
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
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
