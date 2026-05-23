using System.Linq;
using System.Web.Http;
using System.Data.SqlClient;
using System.Data.Entity;
using HMS.Models.ViewModels;
using HMS.Models;
using HMS.Data;

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


    }
}