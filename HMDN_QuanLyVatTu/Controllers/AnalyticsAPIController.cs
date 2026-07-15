using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/analytics")]
    [CustomApiAuthorize("Analytics")]
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
                        .SqlQuery<DashboardOverviewModel>("EXEC [dbo].[sp_DashboardSummary]")
                        .FirstOrDefault() ?? new DashboardOverviewModel();

                    return Ok(overview);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // API 2: Lấy dữ liệu tiến trình Checklist thực tế từ database cho Dashboard
        [HttpGet]
        [Route("checklist-today-progress")]
        public IHttpActionResult GetChecklistTodayProgress(string range = "today")
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Lazy schedule generation for the current month if not yet generated
                    try
                    {
                        new HMDN_QuanLyVatTu.Services.ChecklistSchedulerService().EnsureSchedulesGeneratedForCurrentMonth(db);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"[AnalyticsAPI] Lazy schedule generation failed: {ex.Message}");
                    }

                    DateTime startDate = DateTime.Today;
                    DateTime endDate = DateTime.Today;

                    if (range == "week")
                    {
                        var today = DateTime.Today;
                        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        startDate = today.AddDays(-1 * diff);
                        endDate = startDate.AddDays(6);
                    }
                    else if (range == "month")
                    {
                        var today = DateTime.Today;
                        startDate = new DateTime(today.Year, today.Month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                    }
                    else if (range == "quarter")
                    {
                        var today = DateTime.Today;
                        int quarter = (today.Month - 1) / 3 + 1;
                        startDate = new DateTime(today.Year, (quarter - 1) * 3 + 1, 1);
                        endDate = startDate.AddMonths(3).AddDays(-1);
                    }
                    else if (range == "year")
                    {
                        var today = DateTime.Today;
                        startDate = new DateTime(today.Year, 1, 1);
                        endDate = new DateTime(today.Year, 12, 31);
                    }

                    DateTime startRange = startDate.Date;
                    DateTime endRange = endDate.Date.AddDays(1);

                    // Gọi dịch vụ thống kê tập trung để tính toán chỉ số
                    var metrics = HMDN_QuanLyVatTu.Services.ChecklistAnalyticsService.GetProgressMetrics(db, startRange, endRange);

                    var progress = new
                    {
                        Range = range,
                        TotalSchedules = metrics.TotalSchedules,
                        DoneCount = metrics.DoneCount,
                        PendingCount = metrics.PendingCount,
                        OverdueCount = metrics.OverdueCount
                    };
                    return Ok(progress);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // API 3: Biểu đồ Đường - Tần suất ca bảo trì sửa chữa phân bổ theo 12 tháng
        [HttpGet]
        [Route("maintenance-frequency")]
        public IHttpActionResult GetMaintenanceFrequency(int? year)
        {
            try
            {
                int filterYear = year ?? DateTime.Now.Year;

                using (var db = new HospitalAssetDbContext())
                {
                    var data = db.Database
                        .SqlQuery<MaintenanceFrequencyModel>(
                            "EXEC [dbo].[sp_Dashboard_GetMaintenanceFrequency] @Year",
                            new SqlParameter("@Year", filterYear)
                        )
                        .ToList();

                    return Ok(data);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // API 4: Biểu đồ Cột - Thống kê tổng chi phí sửa chữa theo Loại nhóm thiết bị y tế
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
                            "EXEC [dbo].[sp_GetMaintenanceCostsByYear] @Year",
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

        // API 5: Lấy danh sách nguồn dữ liệu đổ vào Combobox Bộ lọc
        [HttpGet]
        [Route("filter-lookups")]
        public IHttpActionResult GetFilterLookups()
        {
            using (var db = new HospitalAssetDbContext())
            {
                // Sửa lỗi CS1061: Chuyển đổi linh hoạt dùng class gốc hoặc tạo struct nặc danh nếu thiếu Model
                var departments = new List<FilterLookupModel>();
                var groups = new List<FilterLookupModel>();

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

                        // Đọc kết quả 1 (Khoa phòng)
                        departments = objectContext.Translate<FilterLookupModel>(reader).ToList();

                        // Chuyển sang kết quả 2 (Nhóm thiết bị)
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
                    if (db.Database.Connection.State == ConnectionState.Open)
                        db.Database.Connection.Close();
                }

                return Ok(new { Departments = departments, Groups = groups });
            }
        }

        // API 6: Báo cáo chi tiết tồn kho tài sản y tế - Lọc động 4 trạng thái dưới SQL
        [HttpGet]
        [Route("inventory-report")]
        public IHttpActionResult GetInventoryReport(int? departmentId = null, int? groupId = null, int? year = null, string status = null)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                    var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                    var paramYear = new SqlParameter("@Year", year.HasValue ? (object)year.Value : DBNull.Value);

                    object statusValue = string.IsNullOrEmpty(status) ? DBNull.Value : (object)status.Trim();
                    var paramStatus = new SqlParameter("@Status", statusValue);

                    var reportList = db.Database
                        .SqlQuery<InventoryReportModel>(
                            "EXEC [dbo].[sp_GetInventoryReport] @DepartmentId, @GroupId, @Year, @Status",
                            paramDept, paramGroup, paramYear, paramStatus
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
    }
}