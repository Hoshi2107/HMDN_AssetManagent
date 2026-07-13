using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using HMS.Models.Inventory;
using HMS.Models.Catalog;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/alerts")]
    [CustomApiAuthorize("Alerts")]
    public class AlertsApiController : ApiController
    {
        private static DateTime? _lastScanTime = null;
        private static readonly object _scanLock = new object();
        private const int SCAN_COOLDOWN_MINUTES = 2;

        // 1. GET: api/alerts/rules
        [HttpGet]
        [Route("rules")]
        public IHttpActionResult GetRules()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    db.Configuration.ProxyCreationEnabled = false;
                    db.Configuration.LazyLoadingEnabled = false;
                    var rules = db.AlertRules.OrderBy(r => r.Id).ToList();
                    return Content(System.Net.HttpStatusCode.OK, rules, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 2. POST: api/alerts/rules
        [HttpPost]
        [Route("rules")]
        public IHttpActionResult SaveRules([FromBody] List<AlertRule> rules)
        {
            if (rules == null) return BadRequest("Dữ liệu quy tắc không hợp lệ.");

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    foreach (var rule in rules)
                    {
                        var dbRule = db.AlertRules.Find(rule.Id);
                        if (dbRule != null)
                        {
                            dbRule.IsActive = rule.IsActive;
                            dbRule.ThresholdDays = rule.ThresholdDays;
                            dbRule.ThresholdCount = rule.ThresholdCount;
                            dbRule.ThresholdPeriodDays = rule.ThresholdPeriodDays;
                            dbRule.Description = rule.Description;
                        }
                    }
                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Cập nhật quy tắc cấu hình thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 3. GET: api/alerts/list
        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAlerts(string tab = "all", int page = 1, int pageSize = 20)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Tự động dọn dẹp cảnh báo đã xử lý quá hạn (30 ngày)
                    PurgeResolvedAlerts(db);

                    // Tự động kiểm tra chẩn đoán (Lazy evaluation)
                    CheckAndRunDiagnostics(db);

                    var list = db.Database.SqlQuery<AlertDisplaySPResult>(
                        "EXEC [dbo].[sp_GetAlertList] @Tab, @Page, @PageSize",
                        new System.Data.SqlClient.SqlParameter("@Tab", tab),
                        new System.Data.SqlClient.SqlParameter("@Page", page),
                        new System.Data.SqlClient.SqlParameter("@PageSize", pageSize)
                    ).ToList()
                    .Select(a => new AlertDisplayDTO
                    {
                        Id = a.Id,
                        AlertRuleId = a.AlertRuleId,
                        RuleCode = a.RuleCode,
                        RuleType = a.RuleType,
                        InventoryId = a.InventoryId,
                        AssetCode = a.AssetCode,
                        ItemName = a.ItemName,
                        Brand = a.Brand,
                        Model = a.Model,
                        Title = a.Title,
                        Body = a.Body,
                        Severity = a.Severity,
                        IsResolved = a.IsResolved,
                        LocationName = a.LocationName,
                        DepartmentName = a.DepartmentName,
                        WarrantyExpiryDate = a.WarrantyExpiryDate,
                        CreatedAtStr = FormatDateFriendly(a.CreatedAt),
                        CreatedTimeStr = a.CreatedAt.ToString("HH:mm")
                    }).ToList();

                    // Lấy số lượng từng loại để hiển thị tab badge
                    var counts = new
                    {
                        All = db.Alerts.Count(a => !a.IsResolved),
                        Danger = db.Alerts.Count(a => !a.IsResolved && a.Severity == "danger"),
                        Warning = db.Alerts.Count(a => !a.IsResolved && a.Severity == "warning"),
                        Info = db.Alerts.Count(a => !a.IsResolved && a.Severity == "info")
                    };

                    return Content(System.Net.HttpStatusCode.OK, new { alerts = list, counts = counts }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4. POST: api/alerts/resolve/{id}
        [HttpPost]
        [Route("resolve/{id}")]
        public IHttpActionResult ResolveAlert(long id)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alert = db.Alerts.Find(id);
                    if (alert == null) return NotFound();

                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.Now;

                    var userId = HttpContext.Current?.Session?["UserId"] as int? ?? 1;
                    alert.ResolvedBy = userId;

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Xác nhận xử lý cảnh báo thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4.5. POST: api/alerts/resolve-multiple
        [HttpPost]
        [Route("resolve-multiple")]
        public IHttpActionResult ResolveMultipleAlerts([FromBody] List<long> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("Danh sách ID không hợp lệ.");

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alerts = db.Alerts.Where(a => ids.Contains(a.Id) && !a.IsResolved).ToList();
                    if (!alerts.Any()) return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Không có cảnh báo nào cần xử lý." }, Configuration.Formatters.JsonFormatter);

                    var userId = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    foreach (var alert in alerts)
                    {
                        alert.IsResolved = true;
                        alert.ResolvedAt = DateTime.Now;
                        alert.ResolvedBy = userId;
                    }

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Xác nhận xử lý hàng loạt thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4.6. POST: api/alerts/liquidate/{id}
        [HttpPost]
        [Route("liquidate/{id}")]
        public IHttpActionResult LiquidateAsset(long id)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alert = db.Alerts.Find(id);
                    if (alert == null) return NotFound();

                    if (alert.IsResolved)
                    {
                        return BadRequest("Cảnh báo này đã được xử lý từ trước.");
                    }

                    var inventory = db.Inventories.Find(alert.InventoryId);
                    if (inventory == null) return NotFound();

                    // Check active maintenance tasks (Status is not 'closed')
                    bool hasActiveMaintenance = db.MaintenanceLogs.Any(l => l.InventoryId == alert.InventoryId && l.Status != "closed");

                    // Check active ticket/operational tasks (tickets that are linked to this inventory and are not approved or rejected)
                    bool hasActiveTickets = false;
                    if (inventory.IdTicket.HasValue)
                    {
                        var ticket = db.Tickets.Find(inventory.IdTicket.Value);
                        if (ticket != null && ticket.Status != "APPROVED" && ticket.Status != "REJECTED")
                        {
                            hasActiveTickets = true;
                        }
                    }

                    if (hasActiveMaintenance || hasActiveTickets)
                    {
                        string msg = "Không thể thanh lý thiết bị y tế này vì đang có ";
                        if (hasActiveMaintenance && hasActiveTickets)
                            msg += "ca bảo trì chưa đóng và phiếu yêu cầu chưa duyệt.";
                        else if (hasActiveMaintenance)
                            msg += "ca bảo trì chưa đóng.";
                        else
                            msg += "phiếu yêu cầu chưa duyệt.";

                        return BadRequest(msg);
                    }

                    // Perform liquidation
                    inventory.LifeStatus = "disposed";
                    inventory.SuspendedAt = DateTime.Now;
                    inventory.SuspendReason = "Thanh lý thiết bị do lỗi lặp lại nhiều lần (Trung tâm Cảnh báo)";
                    inventory.UpdatedAt = DateTime.Now;
                    inventory.UpdatedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    // Resolve the alert
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.Now;
                    alert.ResolvedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Đã đề xuất thanh lý thiết bị và đóng cảnh báo thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4.7. POST: api/alerts/extend-warranty/{id}
        [HttpPost]
        [Route("extend-warranty/{id}")]
        public IHttpActionResult ExtendWarranty(long id)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alert = db.Alerts.Find(id);
                    if (alert == null) return NotFound();

                    if (alert.IsResolved)
                    {
                        return BadRequest("Cảnh báo này đã được xử lý từ trước.");
                    }

                    var inventory = db.Inventories.Find(alert.InventoryId);
                    if (inventory != null)
                    {
                        DateTime currentExpiry = inventory.WarrantyExpiry ?? DateTime.Today;
                        if (currentExpiry < DateTime.Today)
                        {
                            currentExpiry = DateTime.Today;
                        }
                        inventory.WarrantyExpiry = currentExpiry.AddYears(1);
                        inventory.UpdatedAt = DateTime.Now;
                        inventory.UpdatedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;
                    }

                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.Now;
                    alert.ResolvedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Đã gia hạn bảo hành thêm 12 tháng thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4.8. POST: api/alerts/complete-checklist/{id}
        [HttpPost]
        [Route("complete-checklist/{id}")]
        public IHttpActionResult CompleteChecklist(long id, [FromUri] string status = "done")
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alert = db.Alerts.Find(id);
                    if (alert == null) return NotFound();

                    if (alert.IsResolved)
                    {
                        return BadRequest("Cảnh báo này đã được xử lý từ trước.");
                    }

                    var today = DateTime.Today;
                    string targetStatus = status == "skipped" ? "done" : status;
                    db.Database.ExecuteSqlCommand(
                        "UPDATE ChecklistSchedules SET Status = @status WHERE InventoryId = @invId AND Status = 'pending' AND DueDate < @today",
                        new System.Data.SqlClient.SqlParameter("@status", targetStatus),
                        new System.Data.SqlClient.SqlParameter("@invId", alert.InventoryId),
                        new System.Data.SqlClient.SqlParameter("@today", today)
                    );

                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.Now;
                    alert.ResolvedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = $"Đã cập nhật trạng thái checklist thành '{targetStatus}' và đóng cảnh báo." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4.9. POST: api/alerts/restock/{id}
        [HttpPost]
        [Route("restock/{id}")]
        public IHttpActionResult RestockConsumable(long id, [FromUri] int quantity)
        {
            if (quantity <= 0) return BadRequest("Số lượng nhập kho phải lớn hơn 0.");

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var alert = db.Alerts.Find(id);
                    if (alert == null) return NotFound();

                    if (alert.IsResolved)
                    {
                        return BadRequest("Cảnh báo này đã được xử lý từ trước.");
                    }

                    var inventory = db.Inventories.Find(alert.InventoryId);
                    if (inventory != null)
                    {
                        inventory.Quantity += quantity;
                        inventory.UpdatedAt = DateTime.Now;
                        inventory.UpdatedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;
                    }

                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.Now;
                    alert.ResolvedBy = HttpContext.Current?.Session?["UserId"] as int? ?? 1;

                    db.SaveChanges();
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = $"Đã nhập kho thêm {quantity} đơn vị vật tư tiêu hao thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 5. GET: api/alerts/check-new
        [HttpGet]
        [Route("check-new")]
        public IHttpActionResult CheckNewAlerts()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Lấy tất cả cảnh báo chưa xử lý mới nhất
                    var activeAlerts = db.Alerts
                        .Include(a => a.AlertRule)
                        .Include(a => a.Inventory)
                        .Include(a => a.Inventory.Item)
                        .Where(a => !a.IsResolved)
                        .OrderByDescending(a => a.CreatedAt)
                        .Take(15)
                        .ToList();

                    var result = activeAlerts.Select(a => new
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Body = a.Body,
                        Severity = a.Severity,
                        AssetCode = a.Inventory != null ? a.Inventory.AssetCode : "",
                        ItemName = (a.Inventory != null && a.Inventory.Item != null) ? a.Inventory.Item.Name : "Thiết bị",
                        CreatedAtStr = a.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        CreatedAtFriendly = FormatDateFriendly(a.CreatedAt),
                        RuleCode = a.AlertRule != null ? a.AlertRule.Code : "",
                        InventoryId = a.InventoryId
                    }).ToList();

                    return Content(System.Net.HttpStatusCode.OK, result, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 6. POST: api/alerts/diagnostics
        [HttpPost]
        [Route("diagnostics")]
        public IHttpActionResult TriggerDiagnostics()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    RunDiagnosticsEngine(db);
                    lock (_scanLock)
                    {
                        _lastScanTime = DateTime.Now;
                    }
                    return Content(System.Net.HttpStatusCode.OK, new { success = true, message = "Chạy Engine chẩn đoán cảnh báo thành công." }, Configuration.Formatters.JsonFormatter);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        #region Diagnostic Engine Core
        private void CheckAndRunDiagnostics(HospitalAssetDbContext db)
        {
            bool shouldScan = false;
            lock (_scanLock)
            {
                if (!_lastScanTime.HasValue || (DateTime.Now - _lastScanTime.Value).TotalMinutes >= SCAN_COOLDOWN_MINUTES)
                {
                    shouldScan = true;
                    _lastScanTime = DateTime.Now;
                }
            }

            if (shouldScan)
            {
                try
                {
                    RunDiagnosticsEngine(db);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi chạy Engine chẩn đoán cảnh báo: " + ex.Message);
                }
            }
        }

        private void RunDiagnosticsEngine(HospitalAssetDbContext db)
        {
            db.Database.ExecuteSqlCommand("EXEC [dbo].[sp_RunAlertDiagnostics]");
        }

        private void PurgeResolvedAlerts(HospitalAssetDbContext db)
        {
            try
            {
                var retentionDaysStr = System.Configuration.ConfigurationManager.AppSettings["AlertRetentionDays"];
                int retentionDays = 30;
                if (!string.IsNullOrEmpty(retentionDaysStr) && int.TryParse(retentionDaysStr, out int parsedDays))
                {
                    retentionDays = parsedDays;
                }

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var alertsToPurge = db.Alerts.Where(a => a.IsResolved && a.ResolvedAt < cutoffDate).ToList();
                if (alertsToPurge.Any())
                {
                    db.Alerts.RemoveRange(alertsToPurge);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi khi tự động dọn dẹp cảnh báo đã xử lý: " + ex.Message);
            }
        }
        #endregion

        #region Date Helper
        private static string FormatDateFriendly(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 60)
                return "Vừa xong";
            if (dt.Date == DateTime.Today)
                return "Hôm nay";
            if (dt.Date == DateTime.Today.AddDays(-1))
                return "Hôm qua";
            return dt.ToString("dd/MM/yyyy");
        }
        #endregion
    }

    #region Helper DTOs
    public class AlertDisplayDTO
    {
        public long Id { get; set; }
        public int AlertRuleId { get; set; }
        public string RuleCode { get; set; }
        public string RuleType { get; set; }
        public int InventoryId { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Severity { get; set; }
        public bool IsResolved { get; set; }
        public string LocationName { get; set; }
        public string DepartmentName { get; set; }
        public string WarrantyExpiryDate { get; set; }
        public string CreatedAtStr { get; set; }
        public string CreatedTimeStr { get; set; }
    }

    public class AlertDisplaySPResult
    {
        public long Id { get; set; }
        public int AlertRuleId { get; set; }
        public string RuleCode { get; set; }
        public string RuleType { get; set; }
        public int InventoryId { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Severity { get; set; }
        public bool IsResolved { get; set; }
        public string LocationName { get; set; }
        public string DepartmentName { get; set; }
        public string WarrantyExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OverdueChecklistDTO
    {
        public int InventoryId { get; set; }
        public DateTime DueDate { get; set; }
        public string CycleType { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
    }

    public class LowStockConsumableDTO
    {
        public int InventoryId { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
        public string GroupCode { get; set; }
        public int Quantity { get; set; }
    }
    #endregion
}
