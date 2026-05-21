using HMS.Data;
using HMS.Models;
using HMS.Models.Catalog;
using HMS.Models.ViewModels;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/category")]
    public class CategoryAPIController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET: api/category/groups
        [HttpGet]
        [Route("groups")]
        public IHttpActionResult Groups()
        {
            var data = db.Database
                .SqlQuery<GroupVM>("EXEC sp_GetGroups")
                .ToList();

            return Ok(data);
        }

        // GET: api/category/items/1
        [HttpGet]
        [Route("items/{groupId}")]
        public IHttpActionResult Items(int groupId)
        {
            var data = db.Database
                .SqlQuery<ItemVM>(
                    "EXEC sp_Items_GetByGroupId @GroupId",
                    new SqlParameter("@GroupId", groupId)
                )
                .ToList();

            return Ok(data);
        }

        // GET: api/category/item-inventories/5
        [HttpGet]
        [Route("item-inventories/{itemId}")]
        public IHttpActionResult ItemInventories(int itemId)
        {
            var data = db.Database
                .SqlQuery<ItemInventoryVM>(
                    "EXEC sp_Inventory_ByItemId @ItemId",
                    new SqlParameter("@ItemId", itemId)
                )
                .ToList();

            return Ok(data);
        }

        // PUT: api/category/item/toggle
        [HttpPut]
        [Route("item/toggle")]
        public IHttpActionResult ToggleItem(ToggleItemDTO dto)
        {
            var item = db.Items.FirstOrDefault(x => x.Id == dto.Id);

            if (item == null)
            {
                return NotFound();
            }

            item.IsActive = dto.IsActive;

            db.SaveChanges();

            return Ok(new
            {
                success = true,
                message = "Cập nhật trạng thái thành công"
            });
        }

    }
}
