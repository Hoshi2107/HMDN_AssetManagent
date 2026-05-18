using HMDN_QuanLyVatTu.Models;
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
    public class AnalyticsController : Controller
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["HospitalAssetDB"] != null
            ? ConfigurationManager.ConnectionStrings["HospitalAssetDB"].ConnectionString
            : ConfigurationManager.ConnectionStrings["HospitalAssetDbContext"].ConnectionString;

        // Giao diện chính của Dashboard
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Analytics";
            ViewBag.PageTitle = "Báo cáo & Dashboard Hệ thống";
            return View();
        }

        // API 1: KPI & Tỷ lệ thiết bị (Dùng Store Proc đã tối ưu của bạn)
        [HttpGet]
        public JsonResult GetDashboardSummary()
        {
            var overview = new DashboardOverviewModel();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_DashboardSummary", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Ép kiểu an toàn chống lỗi ép kiểu dữ liệu SQL Server sang C#
                            overview.TotalAssets = reader["TotalAssets"] != DBNull.Value ? Convert.ToInt32(reader["TotalAssets"]) : 0;
                            overview.TotalActive = reader["TotalActive"] != DBNull.Value ? Convert.ToInt32(reader["TotalActive"]) : 0;
                            overview.TotalSuspended = reader["TotalSuspended"] != DBNull.Value ? Convert.ToInt32(reader["TotalSuspended"]) : 0;

                            // Sửa lỗi chí mạng: Dùng float.Parse hoặc Convert.ToDouble kết hợp ép kiểu trung gian
                            overview.ActivePercentage = reader["ActivePercentage"] != DBNull.Value ? Convert.ToDouble(reader["ActivePercentage"]) : 0.0;
                            overview.SuspendedPercentage = reader["SuspendedPercentage"] != DBNull.Value ? Convert.ToDouble(reader["SuspendedPercentage"]) : 0.0;
                        }
                    }
                }
            }
            return Json(overview, JsonRequestBehavior.AllowGet);
        }

        // API 2: Thống kê chi phí sửa chữa theo Loại thiết bị (Biểu đồ Cột)
        [HttpGet]
        public JsonResult GetMaintenanceCosts(int? year)
        {
            int filterYear = year ?? DateTime.Now.Year;
            var costData = new List<MaintenanceCostSummary>();

            string query = @"
                SELECT gr.Name AS CategoryName, SUM(ISNULL(ml.Cost, 0)) AS TotalCost
                FROM MaintenanceLogs ml
                JOIN Inventory inv ON ml.InventoryId = inv.Id
                JOIN Items it ON inv.ItemId = it.Id
                JOIN Groups gr ON it.GroupId = gr.Id
                WHERE YEAR(ml.StartDate) = @Year AND ml.Status = 'closed'
                GROUP BY gr.Id, gr.Name;";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", filterYear);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            costData.Add(new MaintenanceCostSummary
                            {
                                CategoryName = reader["CategoryName"].ToString(),
                                TotalCost = Convert.ToDecimal(reader["TotalCost"])
                            });
                        }
                    }
                }
            }
            return Json(costData, JsonRequestBehavior.AllowGet);
        }

        // API 3: Lấy danh sách bộ lọc Khoa phòng & Loại thiết bị (Groups)
        [HttpGet]
        public JsonResult GetFilterLookups()
        {
            var departments = new List<FilterLookupModel>();
            var groups = new List<FilterLookupModel>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Lấy khoa phòng
                using (SqlCommand cmd = new SqlCommand("SELECT Id, Name FROM Departments WHERE IsActive = 1", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) departments.Add(new FilterLookupModel { Id = Convert.ToInt32(r["Id"]), Name = r["Name"].ToString() });
                }
                // Lấy nhóm loại thiết bị
                using (SqlCommand cmd = new SqlCommand("SELECT Id, Name FROM Groups WHERE IsActive = 1", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) groups.Add(new FilterLookupModel { Id = Convert.ToInt32(r["Id"]), Name = r["Name"].ToString() });
                }
            }
            return Json(new { Departments = departments, Groups = groups }, JsonRequestBehavior.AllowGet);
        }

        // API 4: Truy vấn danh sách Báo cáo tồn kho có bộ lọc Động (Khoa phòng / Loại)
        [HttpGet]
        public JsonResult GetInventoryReport(int? departmentId, int? groupId)
        {
            var reportList = new List<InventoryReportModel>();

            string query = @"
                SELECT inv.Id, inv.AssetCode, it.Name AS ItemName, gr.Name AS GroupName, 
                       dep.Name AS DepartmentName, loc.Name AS LocationName, 
                       inv.Quantity, inv.UnitPrice, inv.TotalPrice, inv.LifeStatus
                FROM Inventory inv
                JOIN Items it ON inv.ItemId = it.Id
                JOIN Groups gr ON it.GroupId = gr.Id
                LEFT JOIN Departments dep ON inv.DepartmentId = dep.Id
                LEFT JOIN Locations loc ON inv.LocationId = loc.Id
                WHERE inv.ApprovalStatus = 'approved'";

            if (departmentId.HasValue) query += " AND inv.DepartmentId = @DeptId";
            if (groupId.HasValue) query += " AND it.GroupId = @GroupId";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (departmentId.HasValue) cmd.Parameters.AddWithValue("@DeptId", departmentId.Value);
                    if (groupId.HasValue) cmd.Parameters.AddWithValue("@GroupId", groupId.Value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reportList.Add(new InventoryReportModel
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                AssetCode = reader["AssetCode"].ToString(),
                                ItemName = reader["ItemName"].ToString(),
                                GroupName = reader["GroupName"].ToString(),
                                DepartmentName = reader["DepartmentName"] != DBNull.Value ? reader["DepartmentName"].ToString() : "Chưa bàn giao",
                                LocationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : "Trong kho chính",
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                                TotalPrice = Convert.ToDecimal(reader["TotalPrice"]),
                                LifeStatus = reader["LifeStatus"].ToString()
                            });
                        }
                    }
                }
            }
            return Json(reportList, JsonRequestBehavior.AllowGet);
        }
    }
}