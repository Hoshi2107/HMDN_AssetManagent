//using HMDN.Models.Location;
//using HMS.Data;
//using HMS.Models;
//using HMS.Models.ViewModels;
//using System;
//using System.Data.Entity;
//using System.Data.SqlClient;
//using System.Linq;
//using System.Web.Http;
//using HMDN_QuanLyVatTu.Controllers;

//namespace HMDN.Controllers.API
//{
//    [RoutePrefix("api/location")]
//    [CustomApiAuthorize("Locations")]
//    public class LocationAPIController : ApiController
//    {
//        HospitalAssetDbContext db = new HospitalAssetDbContext();

//        [HttpGet]
//        [Route("list")]
//        public IHttpActionResult List()
//        {
//            var data = db.Database
//                .SqlQuery<LocationListVM>(
//                    "SELECT * FROM vw_LocationList"
//                )
//                .ToList();

//            return Ok(data);
//        }

//        [HttpGet]
//        [Route("detail")]
//        public IHttpActionResult Detail(int id)
//        {
//            var data = db.Database
//                .SqlQuery<LocationDetailVM>(
//                    "SELECT * FROM vw_LocationList WHERE Id = @p0",
//                    id
//                )
//                .FirstOrDefault();

//            if (data == null)
//                return NotFound();

//            return Ok(data);
//        }


//        [HttpPost]
//        //[Route("toggle-status")]
//        public IHttpActionResult ToggleStatus(int id)
//        {
//            var loc = db.Locations.Find(id);

//            if (loc == null)
//                return NotFound();

//            loc.IsActive = !loc.IsActive;

//            db.SaveChanges();

//            return Ok();
//        }

//        // =========================
//        // CREATE
//        // =========================
//        [HttpPost]
//        [Route("create")]
//        public IHttpActionResult Create
//        (
//            CreateLocationVM model
//        )
//        {
//            try
//            {
//                db.Database.ExecuteSqlCommand
//                (
//                    @"EXEC sp_Location_Create
//                        @p0,
//                        @p1,
//                        @p2,
//                        @p3",

//                    model.Name,
//                    model.Floor,
//                    model.Building,
//                    model.DepartmentId
//                );

//                return Ok(new
//                {
//                    success = true,
//                    message = "Thêm vị trí thành công"
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        // =========================
//        // UPDATE
//        // =========================
//        [HttpPut]
//        [Route("update")]
//        public IHttpActionResult Update
//        (
//            UpdateLocationVM model
//        )
//        {
//            try
//            {
//                db.Database.ExecuteSqlCommand
//                (
//                    @"EXEC sp_Location_Update
//                        @p0,
//                        @p1,
//                        @p2,
//                        @p3,
//                        @p4",

//                    model.Id,
//                    model.Name,
//                    model.Floor,
//                    model.Building,
//                    model.DepartmentId
//                );

//                return Ok(new
//                {
//                    success = true,
//                    message = "Cập nhật thành công"
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        //Get inventory with location
//        [HttpGet]
//        //[Route("inventory")]
//        public IHttpActionResult Inventory(int id)
//        {
//            var data = db.Database
//                .SqlQuery<LocationInventoryDetailVM>(
//                    "EXEC sp_Location_GetInventoryByLocation @p0",
//                    id
//                )
//                .ToList();

//            if (!data.Any())
//                return NotFound();

//            return Ok(data);
//        }


//    }
//}
