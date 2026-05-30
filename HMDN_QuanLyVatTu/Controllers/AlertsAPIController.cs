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
        public IHttpActionResult GetAlerts(string tab = "all")
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Tự động kiểm tra chẩn đoán (Lazy evaluation)
                    CheckAndRunDiagnostics(db);

                    var list = db.Database.SqlQuery<AlertDisplaySPResult>(
                        "EXEC [dbo].[sp_GetAlertList] @Tab",
                        new System.Data.SqlClient.SqlParameter("@Tab", tab)
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

        // 5. GET: api/alerts/check-new
        [HttpGet]
        [Route("check-new")]
        public IHttpActionResult CheckNewAlerts()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Chạy chẩn đoán (Lazy-Evaluation)
                    CheckAndRunDiagnostics(db);

                    // Lấy tất cả cảnh báo chưa xử lý và chưa được thông báo (IsNotified = false)
                    var unnotified = db.Alerts
                        .Include(a => a.AlertRule)
                        .Include(a => a.Inventory)
                        .Include(a => a.Inventory.Item)
                        .Where(a => !a.IsResolved && !a.IsNotified)
                        .OrderByDescending(a => a.CreatedAt)
                        .ToList();

                    if (unnotified.Any())
                    {
                        foreach (var alert in unnotified)
                        {
                            alert.IsNotified = true;
                        }
                        db.SaveChanges();
                    }

                    var result = unnotified.Select(a => new
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Body = a.Body,
                        Severity = a.Severity,
                        AssetCode = a.Inventory.AssetCode,
                        ItemName = a.Inventory.Item.Name,
                        CreatedAtStr = a.CreatedAt.ToString("HH:mm")
                    }).ToList();

                    return Content(System.Net.HttpStatusCode.OK, result, Configuration.Formatters.JsonFormatter);
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
