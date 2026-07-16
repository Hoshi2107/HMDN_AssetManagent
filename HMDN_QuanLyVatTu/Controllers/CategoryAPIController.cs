using HMS.Data;
using HMS.Models;
using HMS.Models.Catalog;
using HMS.Models.ViewModels;
using System;
using System.Collections.Generic;
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

                var list = db.Database.SqlQuery<ChecklistDefinition>(
                    "EXEC sp_GetChecklistDefinitionsByGroup @GroupId",
                    new SqlParameter("@GroupId", groupId)
                ).ToList();

                if (list.Count > 0)
                {
                    var defIds = list.Select(d => d.Id).ToList();
                    var optionsList = db.ChecklistDefinitionOptions
                        .Where(o => defIds.Contains(o.ChecklistDefinitionId) && o.IsActive)
                        .OrderBy(o => o.SortOrder)
                        .ToList();

                    foreach (var item in list)
                    {
                        item.Options = optionsList.Where(o => o.ChecklistDefinitionId == item.Id).ToList();
                    }
                }

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
                    if (dto.ItemIdValue == null)
                    {
                        return Content(System.Net.HttpStatusCode.BadRequest, new { message = "Loại thiết bị cụ thể là bắt buộc khi chọn phạm vi thiết bị." });
                    }
                }
                else if (scopeVal == ChecklistScopes.Inventory)
                {
                    if (dto.InventoryIdValue == null)
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
                        d.ItemId == dto.ItemIdValue && 
                        d.CheckName.ToLower() == checkNameTrim.ToLower());
                }
                else if (scopeVal == ChecklistScopes.Inventory)
                {
                    isDuplicate = db.ChecklistDefinitions.Any(d => 
                        d.Id != dto.Id && 
                        d.IsActive && 
                        d.Scope == ChecklistScopes.Inventory && 
                        d.InventoryId == dto.InventoryIdValue && 
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

                ChecklistDefinition resultDef = null;

                if (dto.Id == 0)
                {
                    var def = new ChecklistDefinition
                    {
                          Scope = scopeVal,
                          GroupId = dto.GroupId,
                          ItemId = (scopeVal == ChecklistScopes.Item || scopeVal == ChecklistScopes.Inventory) ? dto.ItemIdValue : null,
                          InventoryId = (scopeVal == ChecklistScopes.Inventory) ? dto.InventoryIdValue : null,
                          DefinitionCode = dto.DefinitionCode?.Trim(),
                          CycleType = cycle,
                          CheckName = checkNameTrim,
                          Description = dto.Description?.Trim(),
                          IsRequired = dto.IsRequired,
                          SortOrder = dto.SortOrder,
                          IsActive = dto.IsActive,
                          CreatedAt = DateTime.Now,
                          ValueType = string.IsNullOrEmpty(dto.ValueType) ? "checkbox" : dto.ValueType.Trim().ToLower(),
                          Unit = dto.Unit?.Trim(),
                          ValidationRules = dto.ValidationRules?.Trim(),
                          Severity = string.IsNullOrEmpty(dto.Severity) ? "Information" : dto.Severity.Trim()
                    };
                    db.ChecklistDefinitions.Add(def);
                    db.SaveChanges();
                    resultDef = def;

                    if (dto.Options != null && dto.Options.Count > 0)
                    {
                        foreach (var optDto in dto.Options)
                        {
                            db.ChecklistDefinitionOptions.Add(new ChecklistDefinitionOption
                            {
                                ChecklistDefinitionId = resultDef.Id,
                                Value = optDto.Value?.Trim() ?? "",
                                DisplayText = optDto.DisplayText?.Trim() ?? "",
                                Color = optDto.Color?.Trim(),
                                SortOrder = optDto.SortOrder,
                                IsDefault = optDto.IsDefault,
                                IsActive = optDto.IsActive
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var def = db.ChecklistDefinitions.Find(dto.Id);
                    if (def == null) return NotFound();
                    if (def.Scope == "global") return BadRequest("Không thể sửa hạng mục toàn cục từ đây");

                    def.Scope = scopeVal;
                    def.ItemId = (scopeVal == ChecklistScopes.Item || scopeVal == ChecklistScopes.Inventory) ? dto.ItemIdValue : null;
                    def.InventoryId = (scopeVal == ChecklistScopes.Inventory) ? dto.InventoryIdValue : null;
                    def.DefinitionCode = dto.DefinitionCode?.Trim();
                    def.CycleType = cycle;
                    def.CheckName = checkNameTrim;
                    def.Description = dto.Description?.Trim();
                    def.IsRequired = dto.IsRequired;
                    def.SortOrder = dto.SortOrder;
                    def.IsActive = dto.IsActive;
                    def.ValueType = string.IsNullOrEmpty(dto.ValueType) ? "checkbox" : dto.ValueType.Trim().ToLower();
                    def.Unit = dto.Unit?.Trim();
                    def.ValidationRules = dto.ValidationRules?.Trim();
                    def.Severity = string.IsNullOrEmpty(dto.Severity) ? "Information" : dto.Severity.Trim();

                    db.SaveChanges();
                    resultDef = def;

                    var oldOpts = db.ChecklistDefinitionOptions.Where(o => o.ChecklistDefinitionId == def.Id).ToList();
                    db.ChecklistDefinitionOptions.RemoveRange(oldOpts);
                    db.SaveChanges();

                    if (dto.Options != null && dto.Options.Count > 0)
                    {
                        foreach (var optDto in dto.Options)
                        {
                            db.ChecklistDefinitionOptions.Add(new ChecklistDefinitionOption
                            {
                                ChecklistDefinitionId = resultDef.Id,
                                Value = optDto.Value?.Trim() ?? "",
                                DisplayText = optDto.DisplayText?.Trim() ?? "",
                                Color = optDto.Color?.Trim(),
                                SortOrder = optDto.SortOrder,
                                IsDefault = optDto.IsDefault,
                                IsActive = optDto.IsActive
                            });
                        }
                        db.SaveChanges();
                    }
                }

                // Trigger schedule generation event-driven
                try
                {
                    new HMDN_QuanLyVatTu.Services.ChecklistSchedulerService().TriggerGeneration(db, DateTime.Today);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"[CategoryAPI] Failed to generate schedules after save: {ex.Message}");
                }

                return Ok(new { success = true, message = dto.Id == 0 ? "Thêm hạng mục thành công" : "Cập nhật hạng mục thành công", data = resultDef });
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

                // Check dependency validation against all checklist-related tables referencing ChecklistDefinitions
                bool hasLogs = db.ChecklistLogItems.Any(li => li.DefinitionId == id);
                if (hasLogs)
                {
                    // References exist: block hard deletion, perform soft-delete/deactivation instead
                    def.IsActive = false;
                    db.SaveChanges();
                    TriggerScheduler(db);
                    return Ok(new { 
                        success = true, 
                        deleteMode = "soft delete",
                        message = "Hạng mục này đã có dữ liệu kiểm tra liên quan nên hệ thống đã tự động vô hiệu hóa (xóa mềm) để bảo toàn lịch sử dữ liệu."
                    });
                }

                // No references: hard delete is allowed
                db.ChecklistDefinitions.Remove(def);
                db.SaveChanges();
                TriggerScheduler(db);
                return Ok(new { 
                    success = true, 
                    deleteMode = "hard delete",
                    message = "Đã xóa hoàn toàn hạng mục checklist khỏi hệ thống." 
                });
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
                TriggerScheduler(db);
                return Ok(new { success = true, message = "Cập nhật trạng thái thành công", isActive = def.IsActive });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/category/checklist-definition/inventory/{inventoryId}
        [HttpGet]
        [Route("checklist-definition/inventory/{inventoryId}")]
        public IHttpActionResult GetInventoryChecklistDefinitions(int inventoryId)
        {
            try
            {
                var list = db.ChecklistDefinitions
                    .Where(d => d.Scope == "inventory" && d.InventoryId == inventoryId)
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.Id)
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/category/checklist-definition/copy-default/{inventoryId}
        [HttpPost]
        [Route("checklist-definition/copy-default/{inventoryId}")]
        public IHttpActionResult CopyDefaultChecklists(int inventoryId)
        {
            try
            {
                var inventoryData = db.Database.SqlQuery<InventoryCopyDefaultInfo>(@"
                    SELECT inv.Id, inv.ItemId, it.GroupId
                    FROM Inventory inv
                    JOIN Items it ON inv.ItemId = it.Id
                    WHERE inv.Id = @InventoryId",
                    new SqlParameter("@InventoryId", inventoryId)
                ).FirstOrDefault();

                if (inventoryData == null) return NotFound();

                int itemId = inventoryData.ItemId;
                int groupId = inventoryData.GroupId;

                // Get existing inventory-scoped checklist names for this inventory
                var existingNames = db.ChecklistDefinitions
                    .Where(d => d.Scope == "inventory" && d.InventoryId == inventoryId)
                    .Select(d => d.CheckName.ToLower())
                    .ToList();

                // Get default checklists (group-scoped and item-scoped)
                var defaultChecklists = db.ChecklistDefinitions
                    .Where(d => d.IsActive && (
                        (d.Scope == "group" && d.GroupId == groupId) ||
                        (d.Scope == "item" && d.ItemId == itemId)
                    ))
                    .ToList();

                var addedList = new List<ChecklistDefinition>();
                int maxSortOrder = db.ChecklistDefinitions
                    .Where(d => d.Scope == "inventory" && d.InventoryId == inventoryId)
                    .Select(d => (int?)d.SortOrder)
                    .Max() ?? 0;

                foreach (var dc in defaultChecklists)
                {
                    string checkNameTrim = dc.CheckName.Trim();
                    if (!existingNames.Contains(checkNameTrim.ToLower()))
                    {
                        maxSortOrder++;
                        var newDef = new ChecklistDefinition
                        {
                            Scope = "inventory",
                            GroupId = groupId,
                            ItemId = itemId,
                            InventoryId = inventoryId,
                            CycleType = dc.CycleType,
                            CheckName = checkNameTrim,
                            Description = dc.Description,
                            IsRequired = dc.IsRequired,
                            SortOrder = maxSortOrder,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        db.ChecklistDefinitions.Add(newDef);
                        addedList.Add(newDef);
                        existingNames.Add(checkNameTrim.ToLower());
                    }
                }

                if (addedList.Count > 0)
                {
                    db.SaveChanges();
                    TriggerScheduler(db);
                }

                return Ok(new { success = true, count = addedList.Count, data = addedList });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private void TriggerScheduler(HMS.Data.HospitalAssetDbContext dbContext)
        {
            try
            {
                new HMDN_QuanLyVatTu.Services.ChecklistSchedulerService().TriggerGeneration(dbContext, DateTime.Today);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[CategoryAPI] Failed to trigger schedule generation: {ex.Message}");
            }
        }
    }

    public class InventoryCopyDefaultInfo
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int GroupId { get; set; }
    }

    public class ChecklistDefinitionSaveDTO
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Scope { get; set; }
        
        private int? _itemId;
        public object ItemId 
        { 
            get { return _itemId; }
            set { _itemId = ConvertToNullableInt(value); }
        }

        private int? _inventoryId;
        public object InventoryId 
        { 
            get { return _inventoryId; }
            set { _inventoryId = ConvertToNullableInt(value); }
        }

        [Newtonsoft.Json.JsonIgnore]
        public int? ItemIdValue => _itemId;

        [Newtonsoft.Json.JsonIgnore]
        public int? InventoryIdValue => _inventoryId;

        public string DefinitionCode { get; set; }
        public string CycleType { get; set; }
        public string CheckName { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        public string ValueType { get; set; }
        public string Unit { get; set; }
        public string ValidationRules { get; set; }
        public string Severity { get; set; }
        public List<ChecklistDefinitionOptionDTO> Options { get; set; }

        private int? ConvertToNullableInt(object value)
        {
            if (value == null) return null;
            int? parsed = null;
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (int.TryParse(str, out int val)) parsed = val;
            }
            else
            {
                try
                {
                    parsed = Convert.ToInt32(value);
                }
                catch
                {
                    return null;
                }
            }
            if (parsed <= 0) return null;
            return parsed;
        }
    }

    public class ChecklistDefinitionOptionDTO
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public string DisplayText { get; set; }
        public string Color { get; set; }
        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
    }
}
