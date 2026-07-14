using HMS.Data;
using System;
using System.Collections.Generic;
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
                    ms.InventoryId,
                    ms.ScheduleName,
                    ms.MaintenanceType,
                    ms.LastMaintenanceDate,
                    ms.NextMaintenanceDate,
                    ms.ReminderDays,
                    ms.IsRecurring,
                    ms.RecurringMonths,
                    ms.Status,
                    ms.CreatedAt,
                    inv.AssetCode,
                    ISNULL(it.Name, ms.RenewalName) AS ItemName,
                    it.Model,
                    dep.Name     AS DepartmentName,
                    loc.Name     AS LocationName,
                    u.FullName   AS CreatedByName,
                    DATEDIFF(DAY, GETDATE(), ms.NextMaintenanceDate) AS DaysUntilDue
                FROM MaintenanceSchedules ms
                LEFT JOIN Inventory   inv ON ms.InventoryId   = inv.Id
                LEFT JOIN Items       it  ON inv.ItemId       = it.Id
                LEFT JOIN Departments dep ON inv.DepartmentId = dep.Id
                LEFT JOIN Locations   loc ON inv.LocationId   = loc.Id
                LEFT JOIN Users       u   ON ms.CreatedBy     = u.Id
                WHERE ms.IsActive = 1
            ";

            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND ms.Status = @status";
                parameters.Add(new SqlParameter("@status", status));
            }

            if (!string.IsNullOrEmpty(type))
            {
                sql += " AND ms.MaintenanceType = @type";
                parameters.Add(new SqlParameter("@type", type));
            }

            if (!string.IsNullOrEmpty(fromDate))
            {
                sql += " AND ms.NextMaintenanceDate >= @fromDate";
                parameters.Add(new SqlParameter("@fromDate", DateTime.Parse(fromDate)));
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                sql += " AND ms.NextMaintenanceDate <= @toDate";
                parameters.Add(new SqlParameter("@toDate", DateTime.Parse(toDate)));
            }

            if (!string.IsNullOrEmpty(search))
            {
                sql += @" AND (
                    inv.AssetCode  LIKE @search OR
                    it.Name        LIKE @search OR
                    ms.RenewalName LIKE @search OR
                    ms.ScheduleName LIKE @search
                )";
                parameters.Add(new SqlParameter("@search", "%" + search + "%"));
            }

            sql += " ORDER BY ms.NextMaintenanceDate ASC";

            var data = db.Database
                .SqlQuery<MaintenanceScheduleVM>(sql, parameters.ToArray())
                .ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("create-renewal")]
        public IHttpActionResult CreateRenewal([FromBody] RenewalCreateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.RenewalName))
                return BadRequest("Thiếu tên lịch gia hạn");

            var currentUserId = 1; // TODO: lấy từ User.Identity thực tế của m

            var sql = @"
                INSERT INTO MaintenanceSchedules
                    (ScheduleName, MaintenanceType, RenewalName, LastMaintenanceDate,
                     NextMaintenanceDate, ReminderDays, IsRecurring, RecurringMonths,
                     Status, IsActive, CreatedAt, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES
                    (@ScheduleName, 'renewal', @RenewalName, NULL,
                     @NextMaintenanceDate, @ReminderDays, @IsRecurring, @RecurringMonths,
                     'active', 1, GETDATE(), @CreatedBy)";

            var parameters = new[]
            {
                new SqlParameter("@ScheduleName", dto.RenewalName),
                new SqlParameter("@RenewalName", dto.RenewalName),
                new SqlParameter("@NextMaintenanceDate", dto.NextMaintenanceDate),
                new SqlParameter("@ReminderDays", dto.ReminderDays),
                new SqlParameter("@IsRecurring", dto.IsRecurring),
                new SqlParameter("@RecurringMonths", (object)dto.RecurringMonths ?? DBNull.Value),
                new SqlParameter("@CreatedBy", currentUserId)
            };

            var newId = db.Database.SqlQuery<int>(sql, parameters).First();

            return Ok(new { success = true, id = newId });
        }

        [HttpPost]
        [Route("complete-renewal")]
        public IHttpActionResult CompleteRenewal([FromBody] CompleteRenewalDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest("Thiếu Id");

            var current = db.Database.SqlQuery<MaintenanceScheduleVM>(
                @"SELECT Id, MaintenanceType, IsRecurring, RecurringMonths
          FROM MaintenanceSchedules WHERE Id = @id",
                new SqlParameter("@id", dto.Id)
            ).FirstOrDefault();

            if (current == null)
                return NotFound();

            // Đánh dấu lịch hiện tại đã hoàn thành
            db.Database.ExecuteSqlCommand(
                @"UPDATE MaintenanceSchedules
          SET Status = 'completed', LastMaintenanceDate = NextMaintenanceDate
          WHERE Id = @id",
                new SqlParameter("@id", dto.Id));

            int? newId = null;

            // Nếu có lặp lại -> tự tạo lịch gia hạn kế tiếp
            if (current.IsRecurring && current.RecurringMonths.HasValue && current.RecurringMonths > 0)
            {
                var insertSql = @"
            INSERT INTO MaintenanceSchedules
                (ScheduleName, MaintenanceType, RenewalName, LastMaintenanceDate,
                 NextMaintenanceDate, ReminderDays, IsRecurring, RecurringMonths,
                 Status, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.Id
            SELECT
                ScheduleName, MaintenanceType, RenewalName, NextMaintenanceDate,
                DATEADD(MONTH, @months, NextMaintenanceDate), ReminderDays, IsRecurring, RecurringMonths,
                'active', 1, GETDATE(), CreatedBy
            FROM MaintenanceSchedules
            WHERE Id = @id";

                newId = db.Database.SqlQuery<int>(insertSql,
                    new SqlParameter("@months", current.RecurringMonths.Value),
                    new SqlParameter("@id", dto.Id)
                ).First();
            }

            return Ok(new { success = true, newId = newId });
        }



    }
}