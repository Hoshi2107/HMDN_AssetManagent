using HMS.Data;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/maintain-list")]
    public class MaintainListApiController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetList(
            string status = "",
            string type = "",
            string fromDate = "",
            string toDate = "",
            string search = "")
        {
            var sql = @"
                SELECT 
                    ms.Id,
                    ms.ScheduleName,
                    ms.MaintenanceType,
                    ms.LastMaintenanceDate,
                    ms.NextMaintenanceDate,
                    ms.ReminderDays,
                    ms.IsRecurring,
                    ms.RecurringMonths,
                    ms.Status,
                    ms.IsActive,
                    ms.CreatedAt,
                    inv.AssetCode,
                    it.Name      AS ItemName,
                    it.Model,
                    dep.Name     AS DepartmentName,
                    loc.Name     AS LocationName,
                    u.FullName   AS CreatedByName,
                    DATEDIFF(DAY, GETDATE(), ms.NextMaintenanceDate) AS DaysUntilDue
                FROM MaintenanceSchedules ms
                JOIN Inventory  inv ON ms.InventoryId  = inv.Id
                JOIN Items      it  ON inv.ItemId      = it.Id
                LEFT JOIN Departments dep ON inv.DepartmentId = dep.Id
                LEFT JOIN Locations   loc ON inv.LocationId   = loc.Id
                LEFT JOIN Users       u   ON ms.CreatedBy     = u.Id
                WHERE ms.IsActive = 1
            ";

            if (!string.IsNullOrEmpty(status))
                sql += " AND ms.Status = '" + status + "'";

            if (!string.IsNullOrEmpty(type))
                sql += " AND ms.MaintenanceType = '" + type + "'";

            if (!string.IsNullOrEmpty(fromDate))
                sql += " AND ms.NextMaintenanceDate >= '" + fromDate + "'";

            if (!string.IsNullOrEmpty(toDate))
                sql += " AND ms.NextMaintenanceDate <= '" + toDate + "'";

            if (!string.IsNullOrEmpty(search))
                sql += @" AND (
                    inv.AssetCode LIKE N'%" + search + @"%' OR
                    it.Name       LIKE N'%" + search + @"%' OR
                    ms.ScheduleName LIKE N'%" + search + @"%'
                )";

            sql += " ORDER BY ms.NextMaintenanceDate ASC";

            var data = db.Database
                .SqlQuery<MaintenanceScheduleVM>(sql)
                .ToList();

            return Ok(data);
        }
    }

    //public class MaintenanceScheduleVM
    //{
    //    public int Id { get; set; }
    //    public string ScheduleName { get; set; }
    //    public string MaintenanceType { get; set; }
    //    public DateTime? LastMaintenanceDate { get; set; }
    //    public DateTime NextMaintenanceDate { get; set; }
    //    public int ReminderDays { get; set; }
    //    public bool IsRecurring { get; set; }
    //    public int? RecurringMonths { get; set; }
    //    public string Status { get; set; }
    //    public DateTime CreatedAt { get; set; }
    //    public string AssetCode { get; set; }
    //    public string ItemName { get; set; }
    //    public string Model { get; set; }
    //    public string DepartmentName { get; set; }
    //    public string LocationName { get; set; }
    //    public string CreatedByName { get; set; }
    //    public int DaysUntilDue { get; set; }
    //}
}