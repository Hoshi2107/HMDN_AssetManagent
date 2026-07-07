using HMDN.Models.Department;
using HMS.Data;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using HMDN_QuanLyVatTu.Controllers;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/department")]
    [CustomApiAuthorize("Locations")]
    public class DepartmentAPIController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        [HttpGet]
        [Route("list")]
        public IHttpActionResult List()
        {
            var data = db.Database
                .SqlQuery<DepartmentListVM>(
                    "SELECT * FROM vw_DepartmentList"
                )
                .ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("detail")]
        public IHttpActionResult Detail(int id)
        {
            var data = db.Database
                .SqlQuery<DepartmentListVM>(
                    "SELECT * FROM vw_DepartmentList WHERE Id = @p0",
                    id
                )
                .FirstOrDefault();

            if (data == null)
                return NotFound();

            return Ok(data);
        }

        [HttpPost]
        [Route("togglestatus")]
        public IHttpActionResult ToggleStatus(int id)
        {
            var dept = db.Departments.Find(id);

            if (dept == null)
                return NotFound();

            dept.IsActive = !dept.IsActive;

            db.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(CreateDepartmentVM model)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"EXEC sp_Department_Create
                        @p0,
                        @p1,
                        @p2",
                    model.Code,
                    model.Name,
                    model.Description
                );

                return Ok(new { success = true, message = "Thêm khoa phòng thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("update")]
        public IHttpActionResult Update(UpdateDepartmentVM model)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"EXEC sp_Department_Update
                        @p0,
                        @p1,
                        @p2,
                        @p3",
                    model.Id,
                    model.Code,
                    model.Name,
                    model.Description
                );

                return Ok(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("inventory")]
        public IHttpActionResult Inventory(int id)
        {
            var data = db.Database
                .SqlQuery<DepartmentInventoryDetailVM>(
                    "EXEC sp_Department_GetInventoryByDepartment @p0",
                    id
                )
                .ToList();

            if (!data.Any())
                return NotFound();

            return Ok(data);
        }
    }
}

namespace HMDN.Models.Department
{
    public class DepartmentListVM
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AssetCount { get; set; }
    }

    public class CreateDepartmentVM
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class UpdateDepartmentVM
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class DepartmentInventoryDetailVM
    {
        public string DepartmentCode { get; set; }
        public string DepartmentName { get; set; }
        public int? InventoryId { get; set; }
        public string AssetCode { get; set; }
        public string ItemName { get; set; }
        public string GroupName { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public int? Quantity { get; set; }
        public string LifeStatus { get; set; }
    }
}