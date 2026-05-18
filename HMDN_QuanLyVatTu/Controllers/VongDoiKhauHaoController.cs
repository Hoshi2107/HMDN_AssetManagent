using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HMS.Data;
using System.Data.Entity;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class VongDoiKhauHaoController : Controller
    {
        private HospitalAssetDbContext _context = new HospitalAssetDbContext();

        public ActionResult VongDoiKhauHao()
        {
            var inventories = _context.Inventories.Include(i => i.Item).ToList();
            
            var devices = inventories.Select(i => new {
                dbId = i.Id,
                id = i.AssetCode ?? i.Id.ToString(),
                name = i.Item != null ? i.Item.Name : "Không rõ",
                status = string.IsNullOrEmpty(i.LifeStatus) ? "active" : i.LifeStatus.ToLower(),
                openingValue = i.TotalPrice,
                depreciation = i.TotalPrice - (i.ResidualValue ?? 0),
                closingValue = i.ResidualValue ?? 0,
                replacedBy = (string)null,
                importDate = i.ImportDate.ToString("dd/MM/yyyy"),
                expiryDate = i.ExpiryDate.HasValue ? i.ExpiryDate.Value.ToString("dd/MM/yyyy") : "-",
                warrantyExpiry = i.WarrantyExpiry.HasValue ? i.WarrantyExpiry.Value.ToString("dd/MM/yyyy") : "-"
            }).ToList();

            ViewBag.DevicesJson = Newtonsoft.Json.JsonConvert.SerializeObject(devices);

            return View();
        }

        [HttpPost]
        public JsonResult UpdateStatus(int id, string status, string replacedBy, string reason)
        {
            var inventory = _context.Inventories.FirstOrDefault(i => i.Id == id);
            if (inventory != null)
            {
                inventory.LifeStatus = status;
                
                if (!string.IsNullOrEmpty(reason))
                {
                    inventory.Note = string.IsNullOrEmpty(inventory.Note) ? reason : inventory.Note + " | Lý do: " + reason;
                }
                
                if (status == "replaced" && !string.IsNullOrEmpty(replacedBy))
                {
                    string replacedNote = "Thay thế bởi: " + replacedBy;
                    inventory.Note = string.IsNullOrEmpty(inventory.Note) ? replacedNote : inventory.Note + " | " + replacedNote;
                }

                inventory.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy thiết bị!" });
        }

        [HttpPost]
        public JsonResult CalculateDepreciation(int calculateYear)
        {
            var inventories = _context.Inventories.Where(i => i.TotalPrice > 0).ToList();
            int count = 0;

            foreach (var inv in inventories)
            {
                // Số năm đã sử dụng tính đến năm calculateYear
                int yearsUsed = calculateYear - inv.ImportDate.Year;
                if (yearsUsed < 0) yearsUsed = 0;

                decimal rate = 0;
                if (inv.DepreciationRate.HasValue && inv.DepreciationRate.Value > 0)
                {
                    rate = inv.DepreciationRate.Value / 100m;
                }
                else if (inv.DepreciationYears.HasValue && inv.DepreciationYears.Value > 0)
                {
                    rate = 1m / inv.DepreciationYears.Value;
                }

                if (rate > 0 || inv.DepreciationYears > 0)
                {
                    decimal annualDepreciation = inv.TotalPrice * rate;
                    decimal accumulatedDepreciation = annualDepreciation * yearsUsed;

                    decimal residual = inv.TotalPrice - accumulatedDepreciation;
                    if (residual < 0) residual = 0;

                    for (int y = inv.ImportDate.Year + 1; y <= calculateYear; y++)
                    {
                        var existingLog = _context.DepreciationLogs.FirstOrDefault(l => l.InventoryId == inv.Id && l.Year == y);
                        if (existingLog == null)
                        {
                            int yUsed = y - inv.ImportDate.Year;
                            decimal prevAccumulated = annualDepreciation * (yUsed - 1);
                            decimal openingValue = inv.TotalPrice - prevAccumulated;
                            if (openingValue < 0) openingValue = 0;
                            
                            decimal amt = annualDepreciation;
                            if (openingValue - amt < 0) amt = openingValue;
                            
                            if (amt > 0)
                            {
                                var newLog = new HMDN_QuanLyVatTu.Models.DepreciationLog {
                                    InventoryId = inv.Id,
                                    Year = y,
                                    OpeningValue = openingValue,
                                    DepreciationAmt = amt,
                                    ClosingValue = openingValue - amt,
                                    Note = $"Khấu hao tài sản năm {yUsed} ({(rate*100):0.##}%)",
                                    CalculatedAt = DateTime.Now,
                                    CalculatedBy = 1 // Giả sử user ID = 1
                                };
                                _context.DepreciationLogs.Add(newLog);
                            }
                        }
                    }

                    inv.ResidualValue = residual;
                    inv.UpdatedAt = DateTime.Now;
                    count++;
                }
            }

            if (count > 0)
            {
                _context.SaveChanges();
            }

            return Json(new { success = true, message = $"Đã tính khấu hao tự động và cập nhật Giá trị còn lại cho {count} thiết bị đến năm {calculateYear}!" });
        }

        [HttpGet]
        public JsonResult GetDepreciationLogs(int inventoryId)
        {
            var logs = _context.DepreciationLogs
                .Where(l => l.InventoryId == inventoryId)
                .OrderByDescending(l => l.Year)
                .ToList()
                .Select(l => new {
                    year = l.Year,
                    calculatedAt = l.CalculatedAt.ToString("dd/MM/yyyy HH:mm"),
                    opening = l.OpeningValue,
                    amt = l.DepreciationAmt,
                    closing = l.ClosingValue,
                    note = l.Note
                });
            return Json(new { success = true, logs = logs }, JsonRequestBehavior.AllowGet);
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
}
