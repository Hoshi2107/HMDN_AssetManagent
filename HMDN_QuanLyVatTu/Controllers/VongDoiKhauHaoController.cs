using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HMS.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("VongDoiKhauHao")]
    public class VongDoiKhauHaoController : Controller
    {
        private HospitalAssetDbContext _context = new HospitalAssetDbContext();

        public ActionResult VongDoiKhauHao()
        {
            var approvedIds = _context.Inventories.Where(x => x.ApprovalStatus == "approved").Select(x => x.Id).ToList();
            
            var allApproved = _context.Inventories.Where(x => x.ApprovalStatus == "approved").ToList();

            // Fetch maintenance IDs to exclude them from active/suspended exactly like Dashboard
            var maintenanceIds = _context.Database.SqlQuery<int>(
                "SELECT DISTINCT InventoryId FROM dbo.MaintenanceLogs WHERE Status IN ('open', 'in_progress')"
            ).ToList();

            var rawDevices = _context.Database.SqlQuery<VongDoiKhauHaoDeviceDto>("EXEC sp_VongDoiKhauHao_GetDevices")
                                     .Where(d => approvedIds.Contains(d.dbId))
                                     .ToList();
            
            var devices = rawDevices.Select(i => {
                var inv = allApproved.FirstOrDefault(x => x.Id == i.dbId);
                string exactStatus = "unknown";
                if (inv != null && !string.IsNullOrWhiteSpace(inv.LifeStatus)) {
                    exactStatus = inv.LifeStatus.Trim().ToLower();
                }

                // If device is in maintenance, override status so it isn't counted in 'active' or 'suspended' by the Vue UI
                if (maintenanceIds.Contains(i.dbId)) {
                    exactStatus = "maintenance";
                }

                return new {
                    dbId = i.dbId,
                    id = i.id,
                    name = i.name,
                    status = exactStatus,
                    openingValue = i.openingValue,
                    depreciation = i.depreciation,
                    closingValue = i.closingValue,
                    replacedBy = i.replacedBy,
                    importDate = i.importDate.ToString("dd/MM/yyyy"),
                    expiryDate = i.expiryDate.HasValue ? i.expiryDate.Value.ToString("dd/MM/yyyy") : "-",
                    warrantyExpiry = i.warrantyExpiry.HasValue ? i.warrantyExpiry.Value.ToString("dd/MM/yyyy") : "-"
                };
            }).ToList();

            ViewBag.DevicesJson = Newtonsoft.Json.JsonConvert.SerializeObject(devices);

            // Populate dropdowns using Stored Procedures
            var groups = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetGroups").ToList();
            var departments = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetDepartments").ToList();
            var locations = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetLocations").ToList();

            ViewBag.GroupsJson = Newtonsoft.Json.JsonConvert.SerializeObject(groups);
            ViewBag.DepartmentsJson = Newtonsoft.Json.JsonConvert.SerializeObject(departments);
            ViewBag.LocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(locations);

            var dashboardSummary = _context.Database.SqlQuery<DashboardOverviewModel>("EXEC sp_DashboardSummary").FirstOrDefault();
            if (dashboardSummary != null)
            {
                ViewBag.HospitalMaintenanceCount = dashboardSummary.HospitalMaintenanceCount;
                ViewBag.VendorMaintenanceCount = dashboardSummary.VendorMaintenanceCount;
            }
            else
            {
                ViewBag.HospitalMaintenanceCount = 0;
                ViewBag.VendorMaintenanceCount = 0;
            }

            return View();
        }

        [HttpPost]
        public JsonResult UpdateStatus(int id, string status, string replacedBy, string reason)
        {
            try
            {
                var asset = _context.Inventories.FirstOrDefault(x => x.Id == id);
                if (asset == null) 
                    return Json(new { success = false, message = "Không tìm thấy thiết bị này trong hệ thống!" });

                asset.LifeStatus = status;

                if (status == "replaced" && !string.IsNullOrEmpty(replacedBy))
                {
                    // Lấy Id của thiết bị mới từ AssetCode
                    var newAsset = _context.Inventories.FirstOrDefault(x => x.AssetCode == replacedBy);
                    if (newAsset != null)
                    {
                        asset.ReplacedByInventoryId = newAsset.Id;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Mã thiết bị thay thế không tồn tại!" });
                    }
                }
                else
                {
                    asset.ReplacedByInventoryId = null;
                }

                if (status == "suspended")
                {
                    asset.SuspendedAt = DateTime.Now;
                    asset.SuspendReason = reason;
                }
                else if (status == "disposed")
                {
                    asset.Note = string.IsNullOrEmpty(asset.Note) ? reason : asset.Note + " | Lý do thanh lý: " + reason;
                }
                else if (status == "active")
                {
                    asset.ActivatedAt = DateTime.Now;
                }

                asset.UpdatedAt = DateTime.Now;
                asset.UpdatedBy = 1; // Hệ thống / User hiện tại

                _context.SaveChanges();
                
                return Json(new { success = true, message = "Đã cập nhật trạng thái vòng đời vào cơ sở dữ liệu!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi lưu SQL: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult BatchUpdateStatus(List<string> assetCodes, string status, string replacedBy, string reason)
        {
            try
            {
                if (assetCodes == null || !assetCodes.Any()) 
                    return Json(new { success = false, message = "Chưa chọn thiết bị nào!" });

                var assets = _context.Inventories.Where(x => assetCodes.Contains(x.AssetCode)).ToList();
                
                int? replacedById = null;
                if (status == "replaced" && !string.IsNullOrEmpty(replacedBy))
                {
                    var newAsset = _context.Inventories.FirstOrDefault(x => x.AssetCode == replacedBy);
                    if (newAsset != null) replacedById = newAsset.Id;
                    else return Json(new { success = false, message = "Mã thiết bị thay thế không tồn tại!" });
                }

                foreach (var asset in assets)
                {
                    asset.LifeStatus = status;
                    asset.ReplacedByInventoryId = replacedById;
                    
                    if (status == "suspended")
                    {
                        asset.SuspendedAt = DateTime.Now;
                        asset.SuspendReason = reason;
                    }
                    else if (status == "disposed")
                    {
                        asset.Note = string.IsNullOrEmpty(asset.Note) ? reason : asset.Note + " | Lý do thanh lý: " + reason;
                    }
                    else if (status == "active")
                    {
                        asset.ActivatedAt = DateTime.Now;
                    }

                    asset.UpdatedAt = DateTime.Now;
                    asset.UpdatedBy = 1;
                }

                _context.SaveChanges();
                
                return Json(new { success = true, message = $"Đã cập nhật trạng thái vòng đời cho {assets.Count} thiết bị!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi lưu SQL: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CalculateDepreciation(int calculateYear)
        {
            try
            {
                var count = _context.Database.ExecuteSqlCommand(
                    "EXEC sp_VongDoiKhauHao_CalculateDepreciation @CalculateYear, @CalculatedBy",
                    new SqlParameter("@CalculateYear", calculateYear),
                    new SqlParameter("@CalculatedBy", 1)
                );

                return Json(new { success = true, message = $"Đã tính khấu hao tự động và cập nhật Giá trị còn lại cho {count} thiết bị đến năm {calculateYear}!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetDepreciationLogs(int inventoryId)
        {
            try
            {
                var logs = _context.Database.SqlQuery<DepreciationLogDto>(
                    "EXEC sp_VongDoiKhauHao_GetDepreciationLogs @InventoryId",
                    new SqlParameter("@InventoryId", inventoryId)
                ).ToList().Select(l => new {
                    year = l.Year,
                    calculatedAt = l.CalculatedAt.ToString("dd/MM/yyyy HH:mm"),
                    opening = l.OpeningValue,
                    amt = l.DepreciationAmt,
                    closing = l.ClosingValue,
                    note = l.Note
                });

                return Json(new { success = true, logs = logs }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult AddAsset(string assetCode, string itemName, int groupId, string serialNumber, int quantity, 
                                   int locationId, int departmentId, string importDateStr, string expiryDateStr, 
                                   string warrantyExpiryStr, decimal unitPrice, decimal totalPrice, 
                                   decimal? depreciationRate, int? depreciationYears, string note)
        {
            try
            {
                DateTime importDate = DateTime.ParseExact(importDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime? expiryDate = null;
                if (!string.IsNullOrEmpty(expiryDateStr))
                {
                    expiryDate = DateTime.ParseExact(expiryDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                DateTime? warrantyExpiry = null;
                if (!string.IsNullOrEmpty(warrantyExpiryStr))
                {
                    warrantyExpiry = DateTime.ParseExact(warrantyExpiryStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }

                var newId = _context.Database.SqlQuery<decimal>(
                    @"EXEC sp_VongDoiKhauHao_AddAsset 
                        @AssetCode, @ItemName, @GroupId, @SerialNumber, @Quantity, @LocationId, @DepartmentId, 
                        @ImportDate, @ExpiryDate, @WarrantyExpiry, @UnitPrice, @TotalPrice, 
                        @DepreciationRate, @DepreciationYears, @Note, @CreatedBy",
                    new SqlParameter("@AssetCode", assetCode ?? (object)DBNull.Value),
                    new SqlParameter("@ItemName", itemName ?? (object)DBNull.Value),
                    new SqlParameter("@GroupId", groupId),
                    new SqlParameter("@SerialNumber", serialNumber ?? (object)DBNull.Value),
                    new SqlParameter("@Quantity", quantity),
                    new SqlParameter("@LocationId", locationId),
                    new SqlParameter("@DepartmentId", departmentId),
                    new SqlParameter("@ImportDate", importDate),
                    new SqlParameter("@ExpiryDate", expiryDate ?? (object)DBNull.Value),
                    new SqlParameter("@WarrantyExpiry", warrantyExpiry ?? (object)DBNull.Value),
                    new SqlParameter("@UnitPrice", unitPrice),
                    new SqlParameter("@TotalPrice", totalPrice),
                    new SqlParameter("@DepreciationRate", depreciationRate ?? (object)DBNull.Value),
                    new SqlParameter("@DepreciationYears", depreciationYears ?? (object)DBNull.Value),
                    new SqlParameter("@Note", note ?? (object)DBNull.Value),
                    new SqlParameter("@CreatedBy", 1)
                ).FirstOrDefault();

                return Json(new { success = true, message = "Thêm tài sản mới thành công!", newId = (int)newId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class VongDoiKhauHaoDeviceDto
    {
        public int dbId { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string status { get; set; }
        public decimal openingValue { get; set; }
        public decimal depreciation { get; set; }
        public decimal closingValue { get; set; }
        public string replacedBy { get; set; }
        public DateTime importDate { get; set; }
        public DateTime? expiryDate { get; set; }
        public DateTime? warrantyExpiry { get; set; }
    }

    public class DropdownDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class DepreciationLogDto
    {
        public int Year { get; set; }
        public DateTime CalculatedAt { get; set; }
        public decimal OpeningValue { get; set; }
        public decimal DepreciationAmt { get; set; }
        public decimal ClosingValue { get; set; }
        public string Note { get; set; }
    }
}
