using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using HMS.Models.ViewModels;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/device")]
    public class DeviceController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        //get list of inventory
        [HttpGet]
        public IHttpActionResult List()
        {
            var data = db.Database
                .SqlQuery<InventoryListVM>(
                    "EXEC sp_GetInventoryList"
                )
                .ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("detail")] // /api/device/detail?id=1
        public IHttpActionResult Detail(int id)
        {
            var data = db.Database
                .SqlQuery<InventoryDetailVM>(
                    "SELECT * FROM vw_InventoryFull WHERE Id = @Id",
                    new SqlParameter("@Id", id)
                )
                .FirstOrDefault();

            if (data == null) return NotFound();

            return Ok(data);
        }

        [HttpPost]
        [Route("suspend")]
        public IHttpActionResult Suspend(SuspendDeviceVM model)
        {
            db.Database.ExecuteSqlCommand(
                @"
                UPDATE Inventory
                SET
                    LifeStatus = 'suspended',
                    ErrorReason = @Reason,
                    SuspendedAt = GETDATE()
                WHERE Id = @Id
                ",
                new SqlParameter("@Id", model.Id),
                new SqlParameter("@Reason", model.Reason)
            );

            return Ok(new
            {
                success = true
            });
        }

        [HttpPost]
        [Route("status")]
        public IHttpActionResult Status(ChangeStatusVM model)
        {
            db.Database.ExecuteSqlCommand(
                @"
        UPDATE Inventory
        SET
            LifeStatus = @Status,
            UpdatedAt = GETDATE()
        WHERE Id = @Id
        ",
                new SqlParameter("@Id", model.Id),
                new SqlParameter("@Status", model.Status)
            );

            return Ok();
        }

        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(CreateInventoryVM model)
        {
            //    db.Database.ExecuteSqlCommand(

            //        @"EXEC sp_CreateInventory

            //@AssetCode,
            //@ItemId,
            //@SerialNumber,
            //@Quantity,
            //@DepartmentId,
            //@LocationId,
            //@ImportDate,
            //@ExpiryDate,
            //@WarrantyExpiry,
            //@CheckCycleId,
            //@UnitPrice,
            //@DepreciationRate,
            //@DepreciationYears,
            //@ResidualValue,
            //@ApprovedQuantity,
            //@YearManufactured,
            //@YearInUse,
            //@UsageYears,
            //@AssetCategory,
            //@GroupAssetCode,
            //@AccountingCode,
            //@InsuranceCode,
            //@CountryManufactured,
            //@Manufacturer,
            //@SupplierName,
            //@QrCode,
            //@Note,
            //@CreatedBy,
            //@IdTicket",
            db.Database.ExecuteSqlCommand(@"
6EXEC sp_CreateInventory
    @AssetCode = @AssetCode,
    @ItemId = @ItemId,
    @SerialNumber = @SerialNumber,
    @Quantity = @Quantity,
    @DepartmentId = @DepartmentId,
    @LocationId = @LocationId,
    @ImportDate = @ImportDate,
    @ExpiryDate = @ExpiryDate,
    @WarrantyExpiry = @WarrantyExpiry,
    @CheckCycleId = @CheckCycleId,
    @UnitPrice = @UnitPrice,
    @DepreciationRate = @DepreciationRate,
    @DepreciationYears = @DepreciationYears,
    @ResidualValue = @ResidualValue,
    @ApprovedQuantity = @ApprovedQuantity,
    @YearManufactured = @YearManufactured,
    @YearInUse = @YearInUse,
    @UsageYears = @UsageYears,
    @AssetCategory = @AssetCategory,
    @GroupAssetCode = @GroupAssetCode,
    @AccountingCode = @AccountingCode,
    @InsuranceCode = @InsuranceCode,
    @CountryManufactured = @CountryManufactured,
    @Manufacturer = @Manufacturer,
    @SupplierName = @SupplierName,
    @QrCode = @QrCode,
    @Note = @Note,
    @CreatedBy = @CreatedBy,
    @IdTicket = @IdTicket
",
                    new SqlParameter("@AssetCode", model.AssetCode),
                new SqlParameter("@ItemId", model.ItemId),

                new SqlParameter("@SerialNumber",
                    (object)model.SerialNumber ?? DBNull.Value),

                new SqlParameter("@Quantity", model.Quantity),

                new SqlParameter("@DepartmentId",
                    (object)model.DepartmentId ?? DBNull.Value),

                new SqlParameter("@LocationId",
                    (object)model.LocationId ?? DBNull.Value),

                //new SqlParameter("@ImportDate", model.ImportDate),
                new SqlParameter("@ImportDate",
                    (object)model.ImportDate ?? DBNull.Value),

                new SqlParameter("@ExpiryDate",
                    (object)model.ExpiryDate ?? DBNull.Value),

                new SqlParameter("@WarrantyExpiry",
                    (object)model.WarrantyExpiry ?? DBNull.Value),

                new SqlParameter("@CheckCycleId",
                    (object)model.CheckCycleId ?? DBNull.Value),

                new SqlParameter("@UnitPrice", model.UnitPrice),

                new SqlParameter("@DepreciationRate",
                    (object)model.DepreciationRate ?? DBNull.Value),

                new SqlParameter("@DepreciationYears",
                    (object)model.DepreciationYears ?? DBNull.Value),

                new SqlParameter("@ResidualValue",
                    (object)model.ResidualValue ?? DBNull.Value),

                new SqlParameter("@ApprovedQuantity",
                    (object)model.ApprovedQuantity ?? DBNull.Value),

                new SqlParameter("@YearManufactured",
                    (object)model.YearManufactured ?? DBNull.Value),

                new SqlParameter("@YearInUse",
                    (object)model.YearInUse ?? DBNull.Value),

                new SqlParameter("@UsageYears",
                    (object)model.UsageYears ?? DBNull.Value),

                new SqlParameter("@AssetCategory",
                    (object)model.AssetCategory ?? DBNull.Value),

                new SqlParameter("@GroupAssetCode",
                    (object)model.GroupAssetCode ?? DBNull.Value),

                new SqlParameter("@AccountingCode",
                    (object)model.AccountingCode ?? DBNull.Value),

                new SqlParameter("@InsuranceCode",
                    (object)model.InsuranceCode ?? DBNull.Value),

                new SqlParameter("@CountryManufactured",
                    (object)model.CountryManufactured ?? DBNull.Value),

                new SqlParameter("@Manufacturer",
                    (object)model.Manufacturer ?? DBNull.Value),

                new SqlParameter("@SupplierName",
                    (object)model.SupplierName ?? DBNull.Value),

                new SqlParameter("@QrCode",
                    (object)model.QrCode ?? DBNull.Value),

                new SqlParameter("@Note",
                    (object)model.Note ?? DBNull.Value),

                new SqlParameter("@CreatedBy", model.CreatedBy),

                new SqlParameter("@IdTicket",
                    (object)model.IdTicket ?? DBNull.Value)
            );


            return Ok(new
            {
                success = true
            });
        }

        // ITEM
        [HttpGet]
        [Route("items")]
        public IHttpActionResult Items()
        {
            var data = db.Database.SqlQuery<DropdownVM>(@"
        SELECT
            Id,
            Name
        FROM Items
        WHERE IsActive = 1
        ORDER BY Name
        ").ToList();

            return Ok(data);
        }

        // DEPARTMENT
        [HttpGet]
        [Route("departments")]
        public IHttpActionResult Departments()
        {
            var data = db.Database.SqlQuery<DropdownVM>(@"
        SELECT
            Id,
            Name
        FROM Departments
        WHERE IsActive = 1
        ORDER BY Name
        ").ToList();

            return Ok(data);
        }

        // LOCATION
        [HttpGet]
        [Route("locations")]
        public IHttpActionResult Locations()
        {
            var data = db.Database.SqlQuery<DropdownVM>(@"
        SELECT
            Id,
            Name
        FROM Locations
        WHERE IsActive = 1
        ORDER BY Name
        ").ToList();

            return Ok(data);
        }

        // TICKET
        [HttpGet]
        [Route("tickets")]
        public IHttpActionResult Tickets()
        {
            var data = db.Database.SqlQuery<TicketDropdownVM>(@"
        SELECT
            Id,
            TicketCode
        FROM Tickets
        ORDER BY TicketCode
        ").ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("checkcycles")]
        public IHttpActionResult CheckCycles()
        {
            var data = db.Database.SqlQuery<DropdownVM>(@"
            SELECT
            Id,
            Name + ' (' + CycleType + ')' AS Name
            FROM CheckCycles
        ").ToList();

            return Ok(data);
        }

        // GROUPS
        [HttpGet]
        [Route("groups")]
        public IHttpActionResult Groups()
        {
            var data = db.Database.SqlQuery<DropdownVM>(@"
        SELECT
            Id,
            Name
        FROM Groups
        WHERE IsActive = 1
        ORDER BY SortOrder, Name
        ").ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("update")]
        public IHttpActionResult Update(CreateInventoryVM model)
        {
            db.Database.ExecuteSqlCommand(@"
        UPDATE Inventory
        SET
            AssetCode = @AssetCode,
            ItemId = @ItemId,
            SerialNumber = @SerialNumber,
            Quantity = @Quantity,
            DepartmentId = @DepartmentId,
            LocationId = @LocationId,
            ImportDate = @ImportDate,
            ExpiryDate = @ExpiryDate,
            WarrantyExpiry = @WarrantyExpiry,
            CheckCycleId = @CheckCycleId,
            UnitPrice = @UnitPrice,
            DepreciationRate = @DepreciationRate,
            DepreciationYears = @DepreciationYears,
            ResidualValue = @ResidualValue,
            YearManufactured = @YearManufactured,
            YearInUse = @YearInUse,
            UsageYears = @UsageYears,
            AssetCategory = @AssetCategory,
            GroupAssetCode = @GroupAssetCode,
            AccountingCode = @AccountingCode,
            InsuranceCode = @InsuranceCode,
            CountryManufactured = @CountryManufactured,
            Manufacturer = @Manufacturer,
            SupplierName = @SupplierName,
            QrCode = @QrCode,
            Note = @Note,
            IdTicket = @IdTicket
        WHERE Id = @Id",

                new SqlParameter("@Id", model.Id),

                new SqlParameter("@AssetCode", model.AssetCode),
                new SqlParameter("@ItemId", model.ItemId),

                new SqlParameter("@SerialNumber",
                    (object)model.SerialNumber ?? DBNull.Value),

                new SqlParameter("@Quantity", model.Quantity),

                new SqlParameter("@DepartmentId",
                    (object)model.DepartmentId ?? DBNull.Value),

                new SqlParameter("@LocationId",
                    (object)model.LocationId ?? DBNull.Value),

                new SqlParameter("@ImportDate",
                    (object)model.ImportDate ?? DBNull.Value),

                new SqlParameter("@ExpiryDate",
                    (object)model.ExpiryDate ?? DBNull.Value),

                new SqlParameter("@WarrantyExpiry",
                    (object)model.WarrantyExpiry ?? DBNull.Value),

                new SqlParameter("@CheckCycleId",
                    (object)model.CheckCycleId ?? DBNull.Value),

                new SqlParameter("@UnitPrice", model.UnitPrice),

                new SqlParameter("@DepreciationRate",
                    (object)model.DepreciationRate ?? DBNull.Value),

                new SqlParameter("@DepreciationYears",
                    (object)model.DepreciationYears ?? DBNull.Value),

                new SqlParameter("@ResidualValue",
                    (object)model.ResidualValue ?? DBNull.Value),

                new SqlParameter("@YearManufactured",
                    (object)model.YearManufactured ?? DBNull.Value),

                new SqlParameter("@YearInUse",
                    (object)model.YearInUse ?? DBNull.Value),

                new SqlParameter("@UsageYears",
                    (object)model.UsageYears ?? DBNull.Value),

                new SqlParameter("@AssetCategory",
                    (object)model.AssetCategory ?? DBNull.Value),

                new SqlParameter("@GroupAssetCode",
                    (object)model.GroupAssetCode ?? DBNull.Value),

                new SqlParameter("@AccountingCode",
                    (object)model.AccountingCode ?? DBNull.Value),

                new SqlParameter("@InsuranceCode",
                    (object)model.InsuranceCode ?? DBNull.Value),

                new SqlParameter("@CountryManufactured",
                    (object)model.CountryManufactured ?? DBNull.Value),

                new SqlParameter("@Manufacturer",
                    (object)model.Manufacturer ?? DBNull.Value),

                new SqlParameter("@SupplierName",
                    (object)model.SupplierName ?? DBNull.Value),

                new SqlParameter("@QrCode",
                    (object)model.QrCode ?? DBNull.Value),

                new SqlParameter("@Note",
                    (object)model.Note ?? DBNull.Value),

                new SqlParameter("@IdTicket",
                    (object)model.IdTicket ?? DBNull.Value)
            );
           
            return Ok();
        }

        [HttpGet]
        [Route("history/{inventoryId}")]
        public IHttpActionResult GetHistory(int inventoryId)
        {
            var data = db.MaintenanceLogs
                .Where(x => x.InventoryId == inventoryId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("report-error")]
        public IHttpActionResult ReportError(ReportErrorVM model)
        {
            try
            {
                var log = new MaintenanceLog
                {
                    InventoryId = model.InventoryId,
                    TicketId = model.TicketId,

                    MaintenanceType = "corrective",

                    Title = model.Title,

                    ErrorDescription = model.ErrorDescription,

                    StartDate = DateTime.Now,

                    Status = "open",

                    Priority = model.Priority,

                    //ReportedBy = userId,
                    ReportedBy = model.ReportedBy,

                    RepairStatus = "Open",

                    CreatedAt = DateTime.Now
                };

                //change status to suspended
                var inventory = db.Inventories
                  .FirstOrDefault(x => x.Id == model.InventoryId);

                if (inventory != null)
                {
                    inventory.LifeStatus = "suspended";
                }

                db.MaintenanceLogs.Add(log);
                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Báo lỗi thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


    }
}