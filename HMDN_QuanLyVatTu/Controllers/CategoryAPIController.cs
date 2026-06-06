using HMS.Data;
using HMS.Models;
using HMS.Models.Catalog;
using HMS.Models.ViewModels;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;
using HMDN_QuanLyVatTu.Controllers;
using HMDN_QuanLyVatTu.Models;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/category")]
    [CustomApiAuthorize("Catalog")]
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

        // GET: api/category/checklist-definitions/{groupId}
        [HttpGet]
        [Route("checklist-definitions/{groupId}")]
        public IHttpActionResult GetChecklistDefinitions(int groupId)
        {
            try
            {
                var list = db.ChecklistDefinitions
                    .Where(d => d.Scope == "global" || (d.Scope == "group" && d.GroupId == groupId))
                    .OrderBy(d => d.Scope == "global" ? 0 : 1)
                    .ThenBy(d => d.SortOrder)
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/category/checklist-definition/save
        [HttpPost]
        [Route("checklist-definition/save")]
        public IHttpActionResult SaveDefinition(ChecklistDefinitionSaveDTO dto)
        {
            try
            {
                if (dto == null) return BadRequest("Dữ liệu không hợp lệ");
                if (string.IsNullOrEmpty(dto.CheckName)) return BadRequest("Tên hạng mục không được để trống");

                string cycle = null;
                if (!string.IsNullOrEmpty(dto.CycleType))
                {
                    string lower = dto.CycleType.ToLower();
                    if (lower == "daily" || lower == "weekly" || lower == "monthly" || lower == "yearly")
                    {
                        cycle = lower;
                    }
                }

                if (dto.Id == 0)
                {
                    var def = new ChecklistDefinition
                    {
                         Scope = "group",
                         GroupId = dto.GroupId,
                         CycleType = cycle,
                         CheckName = dto.CheckName.Trim(),
                         Description = dto.Description?.Trim(),
                         IsRequired = dto.IsRequired,
                         SortOrder = dto.SortOrder,
                         IsActive = dto.IsActive,
                         CreatedAt = DateTime.Now
                    };
                    db.ChecklistDefinitions.Add(def);
                    db.SaveChanges();
                    return Ok(new { success = true, message = "Thêm hạng mục thành công", data = def });
                }
                else
                {
                    var def = db.ChecklistDefinitions.Find(dto.Id);
                    if (def == null) return NotFound();
                    if (def.Scope == "global") return BadRequest("Không thể sửa hạng mục toàn cục từ đây");

                    def.CycleType = cycle;
                    def.CheckName = dto.CheckName.Trim();
                    def.Description = dto.Description?.Trim();
                    def.IsRequired = dto.IsRequired;
                    def.SortOrder = dto.SortOrder;
                    def.IsActive = dto.IsActive;

                    db.SaveChanges();
                    return Ok(new { success = true, message = "Cập nhật hạng mục thành công", data = def });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: api/category/checklist-definition/{id}
        [HttpDelete]
        [Route("checklist-definition/{id}")]
        public IHttpActionResult DeleteDefinition(int id)
        {
            try
            {
                var def = db.ChecklistDefinitions.Find(id);
                if (def == null) return NotFound();

                if (def.Scope == "global")
                    return Ok(new { success = false, message = "Không thể xóa hạng mục toàn cục từ đây." });

                bool hasLogs = db.ChecklistLogItems.Any(li => li.DefinitionId == id);
                if (hasLogs)
                {
                    return Ok(new { 
                        success = false, 
                        hasLinkedData = true,
                        message = "Hạng mục này đã có dữ liệu kiểm tra. Không thể xóa. Bạn có muốn vô hiệu hóa?"
                    });
                }

                db.ChecklistDefinitions.Remove(def);
                db.SaveChanges();
                return Ok(new { success = true, message = "Đã xóa hạng mục." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: api/category/checklist-definition/toggle/{id}
        [HttpPut]
        [Route("checklist-definition/toggle/{id}")]
        public IHttpActionResult ToggleDefinition(int id)
        {
            try
            {
                var def = db.ChecklistDefinitions.Find(id);
                if (def == null) return NotFound();

                def.IsActive = !def.IsActive;
                db.SaveChanges();
                return Ok(new { success = true, message = "Cập nhật trạng thái thành công", isActive = def.IsActive });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class ChecklistDefinitionSaveDTO
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string CycleType { get; set; }
        public string CheckName { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
