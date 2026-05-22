using HMS.Data;
using HMS.Models;
using HMS.Models.Catalog;
using HMS.Models.ViewModels;
using System;
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

        // POST: api/category/group/create
        [HttpPost]
        [Route("group/create")]
        public IHttpActionResult Create(GroupCreateDTO dto)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"
            EXEC sp_Group_Create
                @Code = @Code,
                @Name = @Name,
                @Icon = @Icon,
                @Description = @Description,
                @SortOrder = @SortOrder,
                @IsActive = @IsActive
            ",

                    new SqlParameter("@Code", dto.Code),
                    new SqlParameter("@Name", dto.Name),
                    new SqlParameter("@Icon", (object)dto.Icon ?? DBNull.Value),
                    new SqlParameter("@Description", (object)dto.Description ?? DBNull.Value),
                    new SqlParameter("@SortOrder", dto.SortOrder),
                    new SqlParameter("@IsActive", dto.IsActive)
                );

                return Ok(new
                {
                    success = true,
                    message = "Thêm nhóm thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/category/inventories
        [HttpGet]
        [Route("inventories")]
        public IHttpActionResult Inventories()
        {
            var data = db.Database
                .SqlQuery<InventoryListVM>(
                    "EXEC sp_GetInventoryList"
                )
                .ToList();

            return Ok(data);
        }

// PUT: api/category/group/update
        [HttpPut]
        [Route("group/update")]
        public IHttpActionResult Update(GroupUpdateDTO dto)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"
            EXEC sp_Groups_Update
                @Id = @Id,
                @Code = @Code,
                @Name = @Name,
                @Icon = @Icon,
                @Description = @Description,
                @SortOrder = @SortOrder,
                @IsActive = @IsActive
            ",

                    new SqlParameter("@Id", dto.Id),
                    new SqlParameter("@Code", dto.Code),
                    new SqlParameter("@Name", dto.Name),
                    new SqlParameter("@Icon", (object)dto.Icon ?? DBNull.Value),
                    new SqlParameter("@Description", (object)dto.Description ?? DBNull.Value),
                    new SqlParameter("@SortOrder", dto.SortOrder),
                    new SqlParameter("@IsActive", dto.IsActive)
                );

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật nhóm thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/category/item/create
        [HttpPost]
        [Route("item/create")]
        public IHttpActionResult Create(ItemCreateDTO dto)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"
            EXEC sp_Item_Create
                @GroupId = @GroupId,
                @Code = @Code,
                @Name = @Name,
                @Brand = @Brand,
                @Model = @Model,
                @Unit = @Unit,
                @Description = @Description,
                @ImageUrl = @ImageUrl,
                @IsActive = @IsActive
            ",

                    new SqlParameter("@GroupId", dto.GroupId),
                    new SqlParameter("@Code", dto.Code),
                    new SqlParameter("@Name", dto.Name),

                    new SqlParameter("@Brand",
                        (object)dto.Brand ?? DBNull.Value),

                    new SqlParameter("@Model",
                        (object)dto.Model ?? DBNull.Value),

                    new SqlParameter("@Unit", dto.Unit),

                    new SqlParameter("@Description",
                        (object)dto.Description ?? DBNull.Value),

                    new SqlParameter("@ImageUrl",
                        (object)dto.ImageUrl ?? DBNull.Value),

                    new SqlParameter("@IsActive", dto.IsActive)
                );

                return Ok(new
                {
                    success = true,
                    message = "Thêm mẫu thiết bị thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
