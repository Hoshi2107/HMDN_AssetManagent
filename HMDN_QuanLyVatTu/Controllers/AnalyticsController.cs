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
                // Vì Entity Framework SqlQuery xử lý kết quả đơn lẻ tốt hơn, 
                // chúng ta gọi 2 lệnh truy vấn tường minh siêu ngắn từ DB
                var departments = db.Database
                    .SqlQuery<FilterLookupModel>("SELECT Id, Name FROM Departments WHERE IsActive = 1")
                    .ToList();

                var groups = db.Database
                    .SqlQuery<FilterLookupModel>("SELECT Id, Name FROM Groups WHERE IsActive = 1")
                    .ToList();

                return Json(new { Departments = departments, Groups = groups }, JsonRequestBehavior.AllowGet);
            }
        }

        // API 4: Truy vấn danh sách Báo cáo tồn kho có bộ lọc Động qua Store Procedure
        [HttpGet]
        public JsonResult GetInventoryReport(int? departmentId, int? groupId)
        {
            using (var db = new HospitalAssetDbContext())
            {
                // Khởi tạo tham số an toàn, hỗ trợ nhận giá trị NULL nếu không chọn bộ lọc
                var paramDept = new SqlParameter("@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                var paramGroup = new SqlParameter("@GroupId", groupId.HasValue ? (object)groupId.Value : DBNull.Value);

                var reportList = db.Database
                    .SqlQuery<InventoryReportModel>(
                        "EXEC sp_GetInventoryReport @DepartmentId, @GroupId",
                        paramDept,
                        paramGroup
                    )
                    .ToList();

                return Json(reportList, JsonRequestBehavior.AllowGet);
            }
        }
    }
}