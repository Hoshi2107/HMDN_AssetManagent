using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using OfficeOpenXml;
using static HMDN_QuanLyVatTu.Models.InventoryReportModel;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/analytics")]
    public class AnalyticsApiController : ApiController
    {
        // API 1: KPI & Tỷ lệ thiết bị tổng quan
        [HttpGet]
        [Route("dashboard-summary")]
        public IHttpActionResult GetDashboardSummary()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var overview = db.Database
                        .SqlQuery<DashboardOverviewModel>("EXEC sp_DashboardSummary")
                        .FirstOrDefault() ?? new DashboardOverviewModel();

                    return Ok(overview);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // API 2: Thống kê chi phí sửa chữa theo Loại thiết bị
        [HttpGet]
        [Route("maintenance-costs")]
        public IHttpActionResult GetMaintenanceCosts(int? year)
        {
            try
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

                    return Ok(costData);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // API 3: Lấy danh sách bộ lọc Khoa phòng & Loại thiết bị (Đã sửa lỗi xung đột Connection)
        [HttpGet]
        [Route("filter-lookups")]
        public IHttpActionResult GetFilterLookups()
        {
            using (var db = new HospitalAssetDbContext())
            {
                var departments = new List<FilterLookupModel>();
                var groups = new List<FilterLookupModel>();

                // Khởi tạo command chuẩn qua EF để tự động quản lý đóng/mở luồng an toàn
                var cmd = db.Database.Connection.CreateCommand();
                cmd.CommandText = "[dbo].[sp_GetFilterLookups]";
                cmd.CommandType = CommandType.StoredProcedure;

                try
                {
                    if (db.Database.Connection.State == ConnectionState.Closed)
                        db.Database.Connection.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        var objectContext = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)db).ObjectContext;

                        // Đọc tập kết quả số 1 (Departments)
                        departments = objectContext.Translate<FilterLookupModel>(reader).ToList();

                        // Chuyển tập kết quả kế tiếp (Groups)
                        if (reader.NextResult())
                        {
                            groups = objectContext.Translate<FilterLookupModel>(reader).ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
                finally
                {
                    // Đảm bảo đóng kết nối thủ công vì ta tự gọi Connection.Open() ở trên
                    if (db.Database.Connection.State == ConnectionState.Open)
                        db.Database.Connection.Close();
                }

                return Ok(new { Departments = departments, Groups = groups });
            }
        }

        // API 4: Truy vấn danh sách Báo cáo tồn kho tài sản y tế
        [HttpGet]
        [Route("inventory-report")]
        public IHttpActionResult GetInventoryReport(int? departmentId, int? groupId, int? year)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                    var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                    var paramYear = new SqlParameter("@Year", year.HasValue ? (object)year.Value : DBNull.Value);

                    var reportList = db.Database
                        .SqlQuery<InventoryReportModel>(
                            "EXEC sp_GetInventoryReport @DepartmentId, @GroupId, @Year",
                            paramDept,
                            paramGroup,
                            paramYear
                        )
                        .ToList();

                    return Ok(reportList);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Chức năng xuất file Excel giải phóng bộ nhớ RAM Server cấp tốc
        [HttpGet]
        [Route("export-excel")]
        public HttpResponseMessage ExportInventoryToExcel(int? departmentId, int? groupId, int? year)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                    var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                    var paramYear = new SqlParameter("@Year", year.HasValue ? (object)year.Value : DBNull.Value);

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

                        // Khởi tạo cấu trúc văn bản hành chính tiêu chuẩn bệnh viện
                        worksheet.Cells["A1"].Value = "BỆNH VIỆN HOÀN MỸ ĐỒNG NAI";
                        worksheet.Cells["A1"].Style.Font.Bold = true;
                        worksheet.Cells["A2"].Value = $"BÁO CÁO TỒN KHO TÀI SẢN THEO KHOA PHÒNG - NĂM {(year.HasValue ? year.Value.ToString() : "TẤT CẢ")}";
                        worksheet.Cells["A2"].Style.Font.Size = 12;
                        worksheet.Cells["A2"].Style.Font.Bold = true;

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
                        var groups = rawList.GroupBy(x => string.IsNullOrEmpty(x.DepartmentName) ? "Kho trung tâm / Chưa bàn giao" : x.DepartmentName);
                        List<string> subtotalCells = new List<string>();

                        foreach (var g in groups)
                        {
                            worksheet.Cells[row, 1, row, 8].Merge = true;
                            worksheet.Cells[row, 1].Value = $"📦 KHOA / PHÒNG: {g.Key.ToUpper()}";
                            worksheet.Cells[row, 1].Style.Font.Bold = true;
                            worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 242, 255));
                            row++;

                            int startGroupRow = row;

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

                            worksheet.Cells[row, 1].Value = $" Cộng chi phí {g.Key}:";
                            worksheet.Cells[row, 1].Style.Font.Bold = true;
                            worksheet.Cells[row, 1, row, 4].Merge = true;
                            worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                            worksheet.Cells[row, 5].Formula = $"SUM(E{startGroupRow}:E{row - 1})";
                            worksheet.Cells[row, 7].Formula = $"SUM(G{startGroupRow}:G{row - 1})";

                            subtotalCells.Add($"G{row}");

                            worksheet.Cells[row, 5].Style.Font.Bold = true;
                            worksheet.Cells[row, 7].Style.Font.Bold = true;
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                            worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                            worksheet.Cells[row, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[row, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                            row += 2;
                        }

                        worksheet.Cells[row, 1].Value = "TỔNG CỘNG TOÀN BỆNH VIỆN";
                        worksheet.Cells[row, 1].Style.Font.Bold = true;
                        worksheet.Cells[row, 1, row, 6].Merge = true;
                        worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                        if (subtotalCells.Count > 0)
                            worksheet.Cells[row, 7].Formula = string.Join("+", subtotalCells);
                        else
                            worksheet.Cells[row, 7].Value = 0;

                        worksheet.Cells[row, 7].Style.Font.Bold = true;
                        worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                        worksheet.Cells[row, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                        for (int col = 1; col <= 8; col++)
                        {
                            worksheet.Cells[row, col].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Double;
                        }

                        if (worksheet.Dimension != null)
                            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                        // ĐÃ TỐI ƯU: Khởi tạo stream bọc trong mảng byte và cho phép tự hủy vùng nhớ
                        var fileBytes = package.GetAsByteArray();
                        var response = new HttpResponseMessage(HttpStatusCode.OK);

                        response.Content = new ByteArrayContent(fileBytes);
                        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = $"BaoCaoTonKho_HoanMy_{DateTime.Now:yyyyMMdd}.xlsx"
                        };

                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                errorResponse.Content = new StringContent($"Lỗi xuất file: {ex.Message}");
                return errorResponse;
            }
        }
    }
}