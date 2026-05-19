using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OfficeOpenXml;
using static HMDN_QuanLyVatTu.Models.InventoryReportModel;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/analytics")]
    public class AnalyticsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Analytics";
            ViewBag.PageTitle = "Báo cáo & Dashboard Hệ thống";
            return View();
        }

        // API 1: KPI & Tỷ lệ thiết bị (Gọi Stored Procedure bằng SqlQuery)
        [HttpGet]
        public JsonResult GetDashboardSummary()
        {
            using (var db = new HospitalAssetDbContext())
            {
                var overview = db.Database
                    .SqlQuery<DashboardOverviewModel>("EXEC sp_DashboardSummary")
                    .FirstOrDefault() ?? new DashboardOverviewModel();

                return Json(overview, JsonRequestBehavior.AllowGet);
            }
        }

        // API 2: Thống kê chi phí sửa chữa theo Loại thiết bị (Biểu đồ Cột)
        [HttpGet]
        public JsonResult GetMaintenanceCosts(int? year)
        {
            int filterYear = year ?? DateTime.Now.Year;

            using (var db = new HospitalAssetDbContext())
            {
                var costData = db.Database
                    .SqlQuery<MaintenanceCostSummary>(
                        "EXEC sp_GetMaintenanceCostsByYear @Year",
                        new SqlParameter("@Year", filterYear)
                    )
                    .ToList();

                return Json(costData, JsonRequestBehavior.AllowGet);
            }
        }

        // API 3: Lấy danh sách bộ lọc Khoa phòng & Loại thiết bị (Groups)
        [HttpGet]
        public JsonResult GetFilterLookups()
        {
            using (var db = new HospitalAssetDbContext())
            {
                var departments = db.Database
                    .SqlQuery<FilterLookupModel>("SELECT Id, Name FROM Departments WHERE IsActive = 1 ORDER BY Name")
                    .ToList();

                var groups = db.Database
                    .SqlQuery<FilterLookupModel>("SELECT Id, Name FROM Groups WHERE IsActive = 1 ORDER BY SortOrder")
                    .ToList();

                return Json(new { Departments = departments, Groups = groups }, JsonRequestBehavior.AllowGet);
            }
        }

        // API 4: Truy vấn danh sách Báo cáo tồn kho tài sản y tế
        [HttpGet]
        public JsonResult GetInventoryReport(int? departmentId, int? groupId, int? year)
        {
            using (var db = new HospitalAssetDbContext())
            {
                var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                var paramYear = new SqlParameter("@Year", year.HasValue ? (object)year.Value : DBNull.Value);

                var reportList = db.Database
                    .SqlQuery<InventoryReportModel>(
                        "EXEC sp_GetInventoryReport @DepartmentId, @GroupId, @Year", // Khớp chuẩn SP 3 tham số
                        paramDept,
                        paramGroup,
                        paramYear
                    )
                    .ToList();

                return Json(reportList, JsonRequestBehavior.AllowGet);
            }
        }

        // CHỨC NĂNG XUẤT FILE: Trích xuất tệp Excel phân nhóm Khoa phòng rút gọn, chuyên nghiệp
        [HttpGet]
        public ActionResult ExportInventoryToExcel(int? departmentId, int? groupId, int? year)
        {
            using (var db = new HospitalAssetDbContext())
            {
                // Khởi tạo đầy đủ và an toàn 3 tham số lọc điều kiện
                var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                var paramYear = new SqlParameter("@Year", year.HasValue ? (object)year.Value : DBNull.Value);

                // SỬA ĐỔI CHÍNH XÁC: Đưa @Year trực tiếp vào chuỗi lệnh EXEC sp_GetInventoryReport
                var rawList = db.Database
                    .SqlQuery<InventoryReportModel>(
                        "EXEC sp_GetInventoryReport @DepartmentId, @GroupId, @Year",
                        paramDept,
                        paramGroup,
                        paramYear
                    )
                    .ToList();

                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Báo cáo tồn kho");
                    worksheet.View.ShowGridLines = true;

                    // Tiêu đề Biên bản kiểm kê
                    worksheet.Cells["A1"].Value = "BỆNH VIỆN HOÀN MỸ ĐỒNG NAI";
                    worksheet.Cells["A1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = $"BÁO CÁO TỒN KHO TÀI SẢN THEO KHOA PHÒNG - NĂM {(year.HasValue ? year.Value.ToString() : "TẤT CẢ")}";
                    worksheet.Cells["A2"].Style.Font.Size = 12;
                    worksheet.Cells["A2"].Style.Font.Bold = true;

                    // Thanh Tiêu đề Cột (Dòng số 4)
                    string[] headers = { "Mã tài sản", "Tên thiết bị y tế", "Loại nhóm", "Vị trí", "SL tồn", "Đơn giá (VNĐ)", "Thành tiền (VNĐ)", "Trạng thái" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[4, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 102, 204));
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }

                    int row = 5;

                    // Thực hiện phân nhóm dữ liệu bằng LINQ GroupBy
                    var groups = rawList.GroupBy(x => string.IsNullOrEmpty(x.DepartmentName) ? "Kho trung tâm / Chưa bàn giao" : x.DepartmentName);

                    foreach (var g in groups)
                    {
                        // In tiêu đề phân nhóm Khoa phòng
                        worksheet.Cells[row, 1, row, 8].Merge = true;
                        worksheet.Cells[row, 1].Value = $"📦 KHOA / PHÒNG: {g.Key.ToUpper()}";
                        worksheet.Cells[row, 1].Style.Font.Bold = true;
                        worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 242, 255));
                        row++;

                        int startGroupRow = row;

                        // Đổ chi tiết thiết bị thuộc khoa phòng ban
                        foreach (var item in g)
                        {
                            worksheet.Cells[row, 1].Value = item.AssetCode;
                            worksheet.Cells[row, 2].Value = item.ItemName;
                            worksheet.Cells[row, 3].Value = item.GroupName ?? "Chưa phân loại";
                            worksheet.Cells[row, 4].Value = item.LocationName ?? "Chưa chỉ định";
                            worksheet.Cells[row, 5].Value = item.Quantity;
                            worksheet.Cells[row, 6].Value = item.UnitPrice;
                            worksheet.Cells[row, 7].Value = item.TotalPrice;
                            worksheet.Cells[row, 8].Value = item.LifeStatus == "active" ? "Đang sử dụng tốt" : "Đang báo hỏng";

                            // Định dạng khung số và tiền tệ vi-VN
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0";
                            worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0";

                            worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[row, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[row, 6].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[row, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[row, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            row++;
                        }

                        // Hàng cộng tổng Subtotal của riêng Khoa phòng ban
                        worksheet.Cells[row, 1].Value = $" Cộng chi phí {g.Key}:";
                        worksheet.Cells[row, 1].Style.Font.Bold = true;
                        worksheet.Cells[row, 1, row, 4].Merge = true;
                        worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                        worksheet.Cells[row, 5].Formula = $"SUM(E{startGroupRow}:E{row - 1})";
                        worksheet.Cells[row, 7].Formula = $"SUM(G{startGroupRow}:G{row - 1})";
                        worksheet.Cells[row, 5].Style.Font.Bold = true;
                        worksheet.Cells[row, 7].Style.Font.Bold = true;
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                        worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                        worksheet.Cells[row, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                        row += 2;
                    }

                    // HÀNG TỔNG TOÀN VIỆN (GRAND TOTAL) Ở CUỐI FILE
                    worksheet.Cells[row, 1].Value = "TỔNG CỘNG TOÀN BỆNH VIỆN";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 1, row, 6].Merge = true;
                    worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                    worksheet.Cells[row, 7].Formula = $"SUM(G5:G{row - 1})/2";
                    worksheet.Cells[row, 7].Style.Font.Bold = true;
                    worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                    // Kẻ khung đường đôi kép kế toán tài chính
                    for (int col = 1; col <= 8; col++)
                    {
                        worksheet.Cells[row, col].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Double;
                    }

                    if (worksheet.Dimension != null) worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    byte[] fileBytes = package.GetAsByteArray();
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoTonKho_HoanMy_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }
    }
}