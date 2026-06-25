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
                // Run one-time database migration logic to support inventory-scoped checklists
                try
                {
                    db.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ChecklistDefinitions') AND name = 'InventoryId')
BEGIN
    ALTER TABLE dbo.ChecklistDefinitions ADD InventoryId INT NULL;
    ALTER TABLE dbo.ChecklistDefinitions ADD CONSTRAINT FK_ChecklistDefinitions_Inventory FOREIGN KEY (InventoryId) REFERENCES dbo.Inventory(Id);
END

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_ChecklistDefinitions_Scope')
BEGIN
    ALTER TABLE dbo.ChecklistDefinitions DROP CONSTRAINT [CK_ChecklistDefinitions_Scope];
END
ALTER TABLE dbo.ChecklistDefinitions ADD CONSTRAINT [CK_ChecklistDefinitions_Scope] CHECK ([Scope]='item' OR [Scope]='group' OR [Scope]='global' OR [Scope]='inventory');

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetChecklistForInventory]') AND type in (N'P', N'PC'))
BEGIN
    EXEC('ALTER PROCEDURE [dbo].[sp_GetChecklistForInventory]
        @InventoryId INT,
        @CycleType   VARCHAR(20) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        DECLARE @ItemId INT, @GroupId INT;
        
        SELECT @ItemId = inv.ItemId, @GroupId = gr.Id
        FROM Inventory inv
            JOIN Items  it ON inv.ItemId = it.Id
            JOIN Groups gr ON it.GroupId = gr.Id
        WHERE inv.Id = @InventoryId;

        SELECT cd.*
        FROM ChecklistDefinitions cd
        WHERE cd.IsActive = 1
          AND (cd.CycleType IS NULL OR @CycleType IS NULL OR cd.CycleType = @CycleType)
          AND (
                (cd.Scope = ''global'')
             OR (cd.Scope = ''group'' AND cd.GroupId = @GroupId)
             OR (cd.Scope = ''item''  AND cd.ItemId  = @ItemId)
             OR (cd.Scope = ''inventory'' AND cd.InventoryId = @InventoryId)
              )
        ORDER BY cd.Scope, cd.SortOrder;
    END');
END
");
                }
                catch { }

                var list = db.ChecklistDefinitions
                    .Where(d => d.Scope == ChecklistScopes.Global || ((d.Scope == ChecklistScopes.Group || d.Scope == ChecklistScopes.Item || d.Scope == ChecklistScopes.Inventory) && d.GroupId == groupId))
                    .OrderBy(d => d.Scope == ChecklistScopes.Global ? 0 : d.Scope == ChecklistScopes.Group ? 1 : d.Scope == ChecklistScopes.Item ? 2 : 3)
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

                string scopeVal = string.IsNullOrEmpty(dto.Scope) ? ChecklistScopes.Group : dto.Scope.ToLower();
                if (scopeVal != ChecklistScopes.Group && scopeVal != ChecklistScopes.Item && scopeVal != ChecklistScopes.Global && scopeVal != ChecklistScopes.Inventory)
                {
                    scopeVal = ChecklistScopes.Group;
                }

                if (scopeVal == ChecklistScopes.Item)
                {
                    if (dto.ItemId == null || dto.ItemId <= 0)
                    {
                        return Content(System.Net.HttpStatusCode.BadRequest, new { message = "Loại thiết bị cụ thể là bắt buộc khi chọn phạm vi thiết bị." });
                    }
                }
                else if (scopeVal == ChecklistScopes.Inventory)
                {
                    if (dto.InventoryId == null || dto.InventoryId <= 0)
                    {
                        return Content(System.Net.HttpStatusCode.BadRequest, new { message = "Thiết bị cụ thể (Tài sản) là bắt buộc khi chọn phạm vi thiết bị riêng." });
                    }
                }

                // Check duplicate check name in the same scope of application
                string checkNameTrim = dto.CheckName.Trim();
                bool isDuplicate = false;
                if (scopeVal == ChecklistScopes.Global)
                {
                    isDuplicate = db.ChecklistDefinitions.Any(d => 
                        d.Id != dto.Id && 
                        d.IsActive && 
                        d.Scope == ChecklistScopes.Global && 
                        d.CheckName.ToLower() == checkNameTrim.ToLower());
                }
                else if (scopeVal == ChecklistScopes.Group)
                {
                    isDuplicate = db.ChecklistDefinitions.Any(d => 
                        d.Id != dto.Id && 
                        d.IsActive && 
                        d.Scope == ChecklistScopes.Group && 
                        d.GroupId == dto.GroupId && 
                        d.CheckName.ToLower() == checkNameTrim.ToLower());
                }
                else if (scopeVal == ChecklistScopes.Item)
                {
                    isDuplicate = db.ChecklistDefinitions.Any(d => 
                        d.Id != dto.Id && 
                        d.IsActive && 
                        d.Scope == ChecklistScopes.Item && 
                        d.ItemId == dto.ItemId && 
                        d.CheckName.ToLower() == checkNameTrim.ToLower());
                }
                else if (scopeVal == ChecklistScopes.Inventory)
                {
                    isDuplicate = db.ChecklistDefinitions.Any(d => 
                        d.Id != dto.Id && 
                        d.IsActive && 
                        d.Scope == ChecklistScopes.Inventory && 
                        d.InventoryId == dto.InventoryId && 
                        d.CheckName.ToLower() == checkNameTrim.ToLower());
                }

                if (isDuplicate)
                {
                    return Content(System.Net.HttpStatusCode.BadRequest, new { message = "Tên hạng mục kiểm tra này đã tồn tại trong phạm vi áp dụng." });
                }

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
                          Scope = scopeVal,
                          GroupId = dto.GroupId,
                          ItemId = (scopeVal == ChecklistScopes.Item || scopeVal == ChecklistScopes.Inventory) ? dto.ItemId : null,
                          InventoryId = (scopeVal == ChecklistScopes.Inventory) ? dto.InventoryId : null,
                          CycleType = cycle,
                          CheckName = checkNameTrim,
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

                    def.Scope = scopeVal;
                    def.ItemId = (scopeVal == ChecklistScopes.Item || scopeVal == ChecklistScopes.Inventory) ? dto.ItemId : null;
                    def.InventoryId = (scopeVal == ChecklistScopes.Inventory) ? dto.InventoryId : null;
                    def.CycleType = cycle;
                    def.CheckName = checkNameTrim;
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
        public string Scope { get; set; }
        public int? ItemId { get; set; }
        public int? InventoryId { get; set; }
        public string CycleType { get; set; }
        public string CheckName { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
