using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HMS.Data;
using System.Data.Entity;
using System.Data.SqlClient;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class VongDoiKhauHaoController : Controller
    {
        private HospitalAssetDbContext _context = new HospitalAssetDbContext();

        public ActionResult VongDoiKhauHao()
        {
            var rawDevices = _context.Database.SqlQuery<VongDoiKhauHaoDeviceDto>("EXEC sp_VongDoiKhauHao_GetDevices").ToList();
            
            var devices = rawDevices.Select(i => new {
                dbId = i.dbId,
                id = i.id,
                name = i.name,
                status = i.status,
                openingValue = i.openingValue,
                depreciation = i.depreciation,
                closingValue = i.closingValue,
                replacedBy = i.replacedBy,
                importDate = i.importDate.ToString("dd/MM/yyyy"),
                expiryDate = i.expiryDate.HasValue ? i.expiryDate.Value.ToString("dd/MM/yyyy") : "-",
                warrantyExpiry = i.warrantyExpiry.HasValue ? i.warrantyExpiry.Value.ToString("dd/MM/yyyy") : "-"
            }).ToList();

            ViewBag.DevicesJson = Newtonsoft.Json.JsonConvert.SerializeObject(devices);

            // Populate dropdowns using Stored Procedures
            var groups = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetGroups").ToList();
            var departments = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetDepartments").ToList();
            var locations = _context.Database.SqlQuery<DropdownDto>("EXEC sp_VongDoiKhauHao_GetLocations").ToList();

            ViewBag.GroupsJson = Newtonsoft.Json.JsonConvert.SerializeObject(groups);
            ViewBag.DepartmentsJson = Newtonsoft.Json.JsonConvert.SerializeObject(departments);
            ViewBag.LocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(locations);

            return View();
        }

        [HttpPost]
        public JsonResult UpdateStatus(int id, string status, string replacedBy, string reason)
        {
            try
            {
                _context.Database.ExecuteSqlCommand(
                    "EXEC sp_VongDoiKhauHao_UpdateStatus @Id, @Status, @ReplacedBy, @Reason",
                    new SqlParameter("@Id", id),
                    new SqlParameter("@Status", status ?? (object)DBNull.Value),
                    new SqlParameter("@ReplacedBy", replacedBy ?? (object)DBNull.Value),
                    new SqlParameter("@Reason", reason ?? (object)DBNull.Value)
                );
                
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CalculateDepreciation(int calculateYear)
        {
            try
            {
                var count = _context.Database.SqlQuery<int>(
                    "EXEC sp_VongDoiKhauHao_CalculateDepreciation @CalculateYear, @CalculatedBy",
                    new SqlParameter("@CalculateYear", calculateYear),
                    new SqlParameter("@CalculatedBy", 1)
                ).FirstOrDefault();

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
