using HMS.Data;
using HMS.Models.ViewModels;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/maintenance-schedule")]
    public class MaintenanceScheduleController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET /api/maintenance-schedule/list?inventoryId=5
        [HttpGet]
        [Route("list")]
        public IHttpActionResult List(int inventoryId)
        {
            var data = db.Database
                .SqlQuery<MaintenanceScheduleVM>(
                    "EXEC sp_MaintenanceSchedule_GetByInventory @InventoryId",
                    new SqlParameter("@InventoryId", inventoryId)
                )
                .ToList();

            return Ok(data);
        }

        // POST /api/maintenance-schedule/create
        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(CreateMaintenanceScheduleVM model)
        {
            try
            {
                var result = db.Database
                    .SqlQuery<decimal>(
                        @"EXEC sp_MaintenanceSchedule_Create
                            @InventoryId, @ScheduleName, @MaintenanceType,
                            @LastMaintenanceDate, @NextMaintenanceDate,
                            @ReminderDays, @IsRecurring, @RecurringMonths,
                            @CreatedBy",
                        new SqlParameter("@InventoryId", model.InventoryId),
                        new SqlParameter("@ScheduleName", model.ScheduleName),
                        new SqlParameter("@MaintenanceType", model.MaintenanceType),
                        new SqlParameter("@LastMaintenanceDate", (object)model.LastMaintenanceDate ?? DBNull.Value),
                        new SqlParameter("@NextMaintenanceDate", model.NextMaintenanceDate),
                        new SqlParameter("@ReminderDays", model.ReminderDays),
                        new SqlParameter("@IsRecurring", model.IsRecurring),
                        new SqlParameter("@RecurringMonths", (object)model.RecurringMonths ?? DBNull.Value),
                        new SqlParameter("@CreatedBy", model.CreatedBy)
                    )
                    .FirstOrDefault();

                return Ok(new { success = true, id = result });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST /api/maintenance-schedule/delete
        [HttpPost]
        [Route("delete")]
        public IHttpActionResult Delete(DeleteScheduleVM model)
        {
            db.Database.ExecuteSqlCommand(
                "EXEC sp_MaintenanceSchedule_Delete @Id, @DeletedBy",
                new SqlParameter("@Id", model.Id),
                new SqlParameter("@DeletedBy", model.DeletedBy)
            );

            return Ok(new { success = true });
        }

        // GET /api/maintenance-schedule/upcoming
        // Dùng cho dashboard / alert badge
        [HttpGet]
        [Route("upcoming")]
        public IHttpActionResult Upcoming()
        {
            var data = db.Database
                .SqlQuery<UpcomingMaintenanceVM>(
                    "SELECT * FROM vw_UpcomingMaintenances ORDER BY DaysUntilDue ASC"
                )
                .ToList();

            return Ok(data);
        }
    }
}

// ─── ViewModels ───────────────────────────────────────────────

public class MaintenanceScheduleVM
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public string ScheduleName { get; set; }
    public string MaintenanceType { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurringMonths { get; set; }
    public string Status { get; set; }
    public string CreatedByName { get; set; }
    public int DaysUntilDue { get; set; }
}

public class CreateMaintenanceScheduleVM
{
    public int InventoryId { get; set; }
    public string ScheduleName { get; set; }
    public string MaintenanceType { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurringMonths { get; set; }
    public int CreatedBy { get; set; }
}

public class DeleteScheduleVM
{
    public int Id { get; set; }
    public int DeletedBy { get; set; }
}

public class UpcomingMaintenanceVM
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public string AssetCode { get; set; }
    public string ItemName { get; set; }
    public string DepartmentName { get; set; }
    public string ScheduleName { get; set; }
    public string MaintenanceType { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public int ReminderDays { get; set; }
    public int DaysUntilDue { get; set; }
    public string UrgencyLevel { get; set; }
}