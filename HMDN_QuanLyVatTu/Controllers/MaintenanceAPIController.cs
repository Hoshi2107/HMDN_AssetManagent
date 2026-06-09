using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Web.Http;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/maintenance")]
    [CustomApiAuthorize("Maintenance")]
    public class MaintenanceApiController : ApiController
    {
        // GET api/maintenance/inventory-list
        // Lấy danh sách TẤT CẢ thiết bị trong Inventory kèm số lần sửa chữa
        [HttpGet]
        [Route("inventory-list")]
        public IHttpActionResult GetInventoryList()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Đếm số log cho mỗi InventoryId
                    var logCounts = db.MaintenanceLogs
                        .GroupBy(l => l.InventoryId)
                        .Select(g => new
                        {
                            InventoryId = g.Key,
                            TotalLogs = g.Count(),
                            OpenLogs = g.Count(l => l.Status == "open"),
                            InProgressLogs = g.Count(l => l.Status == "in_progress"),
                            ClosedLogs = g.Count(l => l.Status == "closed"),
                            LastLogDate = g.Max(l => l.CreatedAt),
                            TotalCost = g.Sum(l => l.Cost)
                        })
                        .ToDictionary(x => x.InventoryId);

                    // Project trực tiếp trong query để tránh lazy-load
                    var inventories = db.Inventories
                        .Where(inv => inv.ApprovalStatus == "approved")
                        .OrderBy(inv => inv.Id)
                        .Select(inv => new
                        {
                            inv.Id,
                            inv.AssetCode,
                            ItemName = inv.Item != null ? inv.Item.Name : "N/A",
                            inv.SerialNumber,
                            DepartmentName = inv.Department != null ? inv.Department.Name : "—",
                            LocationName = inv.Location != null ? inv.Location.Name : "—",
                            inv.LifeStatus,
                            inv.ImportDate,
                            inv.UnitPrice
                        })
                        .ToList();

                    var result = inventories.Select(inv =>
                    {
                        logCounts.TryGetValue(inv.Id, out var stats);
                        return new
                        {
                            inv.Id,
                            inv.AssetCode,
                            inv.ItemName,
                            inv.SerialNumber,
                            inv.DepartmentName,
                            inv.LocationName,
                            LifeStatus = (stats != null && stats.OpenLogs > 0) ? "broken" :
                                         (stats != null && stats.InProgressLogs > 0) ? "suspended" :
                                         inv.LifeStatus,
                            ImportDate = inv.ImportDate.ToString("yyyy-MM-dd"),
                            UnitPrice = inv.UnitPrice,
                            // Thống kê sửa chữa
                            TotalLogs = stats != null ? stats.TotalLogs : 0,
                            OpenLogs = stats != null ? stats.OpenLogs : 0,
                            InProgressLogs = stats != null ? stats.InProgressLogs : 0,
                            ClosedLogs = stats != null ? stats.ClosedLogs : 0,
                            LastLogDate = stats != null ? stats.LastLogDate.ToString("yyyy-MM-dd HH:mm") : null,
                            TotalMaintenanceCost = stats != null ? stats.TotalCost : 0
                        };
                    });

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/history?inventoryId=5
        // Lấy TOÀN BỘ lịch sử sửa chữa của 1 thiết bị
        [HttpGet]
        [Route("history")]
        public IHttpActionResult GetHistory(int inventoryId)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Lấy thông tin thiết bị
                    var device = db.Inventories
                        .Where(inv => inv.Id == inventoryId)
                        .Select(inv => new
                        {
                            inv.Id,
                            inv.AssetCode,
                            ItemName = inv.Item != null ? inv.Item.Name : "N/A",
                            Brand = inv.Item != null ? inv.Item.Brand : "N/A",
                            Model = inv.Item != null ? inv.Item.Model : "N/A",
                            inv.SerialNumber,
                            DepartmentName = inv.Department != null ? inv.Department.Name : null,
                            LocationName = inv.Location != null ? inv.Location.Name : null,
                            inv.LifeStatus,
                            ImportDate = inv.ImportDate,
                            WarrantyExpiry = inv.WarrantyExpiry,
                            inv.UnitPrice
                        })
                        .FirstOrDefault();

                    if (device == null)
                        return NotFound();

                    // Lấy tất cả log của thiết bị này
                    var logs = db.MaintenanceLogs
                        .Where(l => l.InventoryId == inventoryId)
                        .OrderByDescending(l => l.CreatedAt)
                        .ToList()
                        .Select(l => new
                        {
                            l.Id,
                            l.InventoryId,
                            l.MaintenanceType,
                            l.Title,
                            l.Description,
                            l.ErrorDescription,
                            l.ActionTaken,
                            l.Cost,
                            l.PartReplaced,
                            l.Vendor,
                            StartDate = l.StartDate.ToString("yyyy-MM-dd"),
                            EndDate = l.EndDate.HasValue ? l.EndDate.Value.ToString("yyyy-MM-dd") : null,
                            l.Status,
                            l.Priority,
                            l.AssignedTo,
                            l.ReportedBy,
                            l.ImageUrls,
                            CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                            l.ClosedBy,
                            ClosedAt = l.ClosedAt.HasValue ? l.ClosedAt.Value.ToString("yyyy-MM-dd HH:mm") : null
                        });

                    return Ok(new
                    {
                        Device = new
                        {
                            device.Id,
                            device.AssetCode,
                            device.ItemName,
                            Brand = string.IsNullOrEmpty(device.Brand) ? "N/A" : device.Brand,
                            Model = string.IsNullOrEmpty(device.Model) ? "N/A" : device.Model,
                            device.SerialNumber,
                            device.DepartmentName,
                            device.LocationName,
                            LifeStatus = logs.Any(l => l.Status == "open") ? "broken" :
                                         logs.Any(l => l.Status == "in_progress") ? "suspended" :
                                         device.LifeStatus,
                            ImportDate = device.ImportDate.ToString("yyyy-MM-dd"),
                            ManufactureYear = device.ImportDate.Year,
                            WarrantyExpiry = device.WarrantyExpiry.HasValue ? device.WarrantyExpiry.Value.ToString("dd/MM/yyyy") : "Không có",
                            Origin = "N/A"
                        },
                        Logs = logs,
                        TotalLogs = logs.Count(),
                        TotalMaintenanceCost = (logs.Sum(l => l.Cost) ?? 0) > 0 ? (logs.Sum(l => l.Cost) ?? 0) : device.UnitPrice,
                        LastMaintenanceDate = logs.Where(l => l.Status == "closed").Max(l => l.EndDate) != null ? logs.Where(l => l.Status == "closed").Max(l => l.EndDate) : (logs.Any() ? logs.Max(l => l.StartDate) : null),
                        Uptime = "98.2%"
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/list
        // Giữ lại: Lấy danh sách toàn bộ nhật ký sửa chữa
        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetList()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var logs = db.MaintenanceLogs
                        .OrderByDescending(l => l.CreatedAt)
                        .ToList();

                    var inventoryIds = logs.Select(l => l.InventoryId).Distinct().ToList();

                    var deviceNames = db.Inventories
                        .Where(inv => inventoryIds.Contains(inv.Id))
                        .Select(inv => new
                        {
                            inv.Id,
                            inv.Item.Name,
                            inv.SerialNumber
                        })
                        .ToDictionary(x => x.Id);

                    var result = logs.Select(l =>
                    {
                        deviceNames.TryGetValue(l.InventoryId, out var device);
                        return new
                        {
                            l.Id,
                            l.InventoryId,
                            l.MaintenanceType,
                            DeviceName = device != null ? device.Name : "N/A",
                            DeviceSerial = device != null ? device.SerialNumber : "N/A",
                            l.Title,
                            l.Description,
                            l.ErrorDescription,
                            l.ActionTaken,
                            l.Cost,
                            l.PartReplaced,
                            l.Vendor,
                            StartDate = l.StartDate.ToString("yyyy-MM-dd"),
                            EndDate = l.EndDate.HasValue ? l.EndDate.Value.ToString("yyyy-MM-dd") : null,
                            l.Status,
                            l.Priority,
                            l.AssignedTo,
                            l.ReportedBy,
                            l.ImageUrls,
                            CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                            l.ClosedBy,
                            ClosedAt = l.ClosedAt.HasValue ? l.ClosedAt.Value.ToString("yyyy-MM-dd HH:mm") : null
                        };
                    });

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/detail?id=1
        // Lấy chi tiết 1 ca sửa chữa
        [HttpGet]
        [Route("detail")]
        public IHttpActionResult GetDetail(int id)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var log = db.MaintenanceLogs.Find(id);
                    if (log == null)
                        return NotFound();

                    // Lấy tên thiết bị
                    var device = db.Inventories
                        .Where(inv => inv.Id == log.InventoryId)
                        .Select(inv => new
                        {
                            inv.Id,
                            inv.Item.Name,
                            inv.SerialNumber,
                            DepartmentName = inv.Department != null ? inv.Department.Name : null,
                            LocationName = inv.Location != null ? inv.Location.Name : null
                        })
                        .FirstOrDefault();

                    return Ok(new
                    {
                        log.Id,
                        log.InventoryId,
                        DeviceName = device != null ? device.Name : "N/A",
                        DeviceSerial = device != null ? device.SerialNumber : "N/A",
                        DeviceDepartment = device != null ? device.DepartmentName : null,
                        DeviceLocation = device != null ? device.LocationName : null,
                        log.MaintenanceType,
                        log.Title,
                        log.Description,
                        log.ErrorDescription,
                        log.ActionTaken,
                        log.Cost,
                        log.PartReplaced,
                        log.Vendor,
                        StartDate = log.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = log.EndDate.HasValue ? log.EndDate.Value.ToString("yyyy-MM-dd") : null,
                        log.Status,
                        log.Priority,
                        log.AssignedTo,
                        log.ReportedBy,
                        log.ImageUrls,
                        CreatedAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        log.ClosedBy,
                        ClosedAt = log.ClosedAt.HasValue ? log.ClosedAt.Value.ToString("yyyy-MM-dd HH:mm") : null
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/maintenance/create
        // Tạo mới 1 ca sửa chữa
        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(MaintenanceLog model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Dữ liệu không hợp lệ.");

                if (model.InventoryId <= 0)
                    return BadRequest("Vui lòng chọn thiết bị.");

                if (string.IsNullOrWhiteSpace(model.Title))
                    return BadRequest("Vui lòng nhập tiêu đề.");

                model.Status = "open";
                model.CreatedAt = DateTime.Now;

                if (model.StartDate == default(DateTime))
                    model.StartDate = DateTime.Now;

                if (model.ReportedBy <= 0)
                    model.ReportedBy = 1; // Mặc định admin

                using (var db = new HospitalAssetDbContext())
                {
                    db.MaintenanceLogs.Add(model);

                    // Thay đổi trạng thái thiết bị thành "broken" (Hỏng) khi có ca báo hỏng mới
                    var inventory = db.Inventories.Find(model.InventoryId);
                    if (inventory != null)
                    {
                        inventory.LifeStatus = "broken";
                    }

                    db.SaveChanges();

                    return Ok(new { success = true, id = model.Id, message = "Tạo ca sửa chữa thành công." });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/maintenance/update-status
        // Cập nhật trạng thái ca sửa chữa (in_progress, closed)
        [HttpPost]
        [Route("update-status")]
        public IHttpActionResult UpdateStatus([FromBody] UpdateMaintenanceStatusDTO dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("Dữ liệu không hợp lệ.");

                using (var db = new HospitalAssetDbContext())
                {
                    var log = db.MaintenanceLogs.Find(dto.Id);
                    if (log == null)
                        return NotFound();

                    log.Status = dto.Status;

                    if (!string.IsNullOrWhiteSpace(dto.ActionTaken))
                        log.ActionTaken = dto.ActionTaken;

                    if (dto.Cost.HasValue)
                        log.Cost = dto.Cost;

                    if (!string.IsNullOrWhiteSpace(dto.PartReplaced))
                        log.PartReplaced = dto.PartReplaced;

                    if (!string.IsNullOrWhiteSpace(dto.Vendor))
                        log.Vendor = dto.Vendor;

                    var inventory = db.Inventories.Find(log.InventoryId);

                    // Nếu đang xử lý
                    if (dto.Status == "in_progress")
                    {
                        if (inventory != null)
                        {
                            inventory.LifeStatus = "suspended"; // Tạm ngưng để sửa
                        }
                    }
                    // Nếu đóng ca
                    else if (dto.Status == "closed")
                    {
                        log.ClosedAt = DateTime.Now;
                        log.ClosedBy = 1; // Mặc định admin
                        log.EndDate = DateTime.Now;

                        // Chuyển lại trạng thái thiết bị thành "active" nếu tất cả ca sửa chữa khác đã đóng
                        if (inventory != null)
                        {
                            bool hasActiveLogs = db.MaintenanceLogs.Any(l => l.InventoryId == log.InventoryId && l.Id != log.Id && l.Status != "closed");
                            if (!hasActiveLogs)
                            {
                                inventory.LifeStatus = "active"; // Đang sử dụng
                            }
                        }

                        // Cập nhật trạng thái phiếu bên phê duyệt thành Hoàn thành (APPROVED)
                        if (log.TicketId.HasValue && log.TicketId.Value > 0)
                        {
                            var ticket = db.Tickets.FirstOrDefault(t => t.Id == log.TicketId.Value);
                            if (ticket != null)
                            {
                                ticket.Status = "APPROVED";
                                ticket.ApprovedBy = log.ClosedBy;
                                ticket.ApprovedAt = DateTime.Now;

                                var tDetails = db.TicketDetails.Where(td => td.TicketId == ticket.Id).ToList();
                                foreach (var td in tDetails)
                                {
                                    td.ApprovalStatus = "approved";
                                    td.ApprovedQuantity = td.Quantity;
                                    td.ApprovalNote = "Đã hoàn thành sửa chữa thiết bị";
                                }
                            }
                        }
                    }

                    db.SaveChanges();
                    return Ok(new { success = true, message = "Cập nhật trạng thái thành công." });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/devices
        // Lấy danh sách thiết bị cho dropdown tạo mới
        [HttpGet]
        [Route("devices")]
        public IHttpActionResult GetDevices()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var devices = db.Inventories
                        .Where(inv => inv.ApprovalStatus == "approved")
                        .Select(inv => new
                        {
                            inv.Id,
                            ItemName = inv.Item != null ? inv.Item.Name : "N/A",
                            inv.SerialNumber,
                            DepartmentName = inv.Department != null ? inv.Department.Name : null
                        })
                        .OrderBy(x => x.ItemName)
                        .ToList();

                    return Ok(devices);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/maintenance/upload-attachment
        [HttpPost]
        [Route("upload-attachment")]
        public IHttpActionResult UploadAttachment()
        {
            try
            {
                var httpRequest = System.Web.HttpContext.Current.Request;
                if (httpRequest.Files.Count == 0)
                {
                    return BadRequest("Không có file nào được tải lên.");
                }

                if (!int.TryParse(httpRequest.Form["InventoryId"], out int inventoryId))
                {
                    return BadRequest("InventoryId không hợp lệ.");
                }

                using (var db = new HospitalAssetDbContext())
                {
                    foreach (string file in httpRequest.Files)
                    {
                        var postedFile = httpRequest.Files[file];
                        if (postedFile != null && postedFile.ContentLength > 0)
                        {
                            using (var binaryReader = new System.IO.BinaryReader(postedFile.InputStream))
                            {
                                byte[] fileData = binaryReader.ReadBytes(postedFile.ContentLength);

                                var attachment = new HMS.Models.Inventory.InventoryAttachment
                                {
                                    InventoryId = inventoryId,
                                    FileName = System.IO.Path.GetFileName(postedFile.FileName),
                                    FileType = postedFile.ContentType,
                                    FileSize = postedFile.ContentLength,
                                    FileData = fileData,
                                    UploadedAt = DateTime.Now
                                };

                                db.InventoryAttachments.Add(attachment);
                            }
                        }
                    }
                    db.SaveChanges();
                    return Ok(new { success = true, message = "Tải file lên thành công." });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/attachments/{inventoryId}
        [HttpGet]
        [Route("attachments/{inventoryId}")]
        public IHttpActionResult GetAttachments(int inventoryId)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var files = db.InventoryAttachments
                        .Where(a => a.InventoryId == inventoryId)
                        .Select(a => new
                        {
                            a.Id,
                            a.FileName,
                            a.FileSize,
                            a.FileType,
                            UploadedAt = a.UploadedAt
                        })
                        .OrderByDescending(a => a.UploadedAt)
                        .ToList()
                        .Select(a => new
                        {
                            a.Id,
                            a.FileName,
                            a.FileSize,
                            a.FileType,
                            UploadedAt = a.UploadedAt.ToString("yyyy-MM-dd HH:mm")
                        });

                    return Ok(files);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/maintenance/download-attachment/{id}
        [HttpGet]
        [Route("download-attachment/{id}")]
        public System.Net.Http.HttpResponseMessage DownloadAttachment(int id)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var attachment = db.InventoryAttachments.Find(id);
                    if (attachment == null)
                    {
                        return Request.CreateResponse(System.Net.HttpStatusCode.NotFound);
                    }

                    var result = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.ByteArrayContent(attachment.FileData)
                    };
                    result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileName = attachment.FileName
                    };
                    result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(attachment.FileType);

                    return result;
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(System.Net.HttpStatusCode.InternalServerError, ex);
            }
        }

        // POST api/maintenance/update-device-status
        // Cập nhật LifeStatus của Inventory (trạng thái hoạt động thiết bị)
        [HttpPost]
        [Route("update-device-status")]
        public IHttpActionResult UpdateDeviceLifeStatus([FromBody] UpdateDeviceStatusDTO dto)
        {
            try
            {
                if (dto == null || dto.InventoryId <= 0)
                    return BadRequest("Dữ liệu không hợp lệ.");

                using (var db = new HospitalAssetDbContext())
                {
                    var inventory = db.Inventories.Find(dto.InventoryId);
                    if (inventory == null)
                        return NotFound();

                    inventory.LifeStatus = dto.LifeStatus;
                    db.SaveChanges();

                    return Ok(new { success = true, message = "Cập nhật trạng thái thành công." });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/maintenance/update-status/{id}
        //[HttpPost]
        //[Route("update-status/{id}")]
        //public IHttpActionResult UpdateLifeStatus(int id, [FromBody] string newStatus)
        //{
        //    try
        //    {
        //        using (var db = new HospitalAssetDbContext())
        //        {
        //            var inventory = db.Inventories.FirstOrDefault(i => i.Id == id);
        //            if (inventory == null) return NotFound();

        //            inventory.LifeStatus = newStatus;
        //            db.SaveChanges();

        //            return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}
    }

    // DTO cho cập nhật trạng thái
    public class UpdateMaintenanceStatusDTO
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public string ActionTaken { get; set; }
        public decimal? Cost { get; set; }
        public string PartReplaced { get; set; }
        public string Vendor { get; set; }
    }

    // DTO cho cập nhật trạng thái hoạt động của thiết bị (Inventory.LifeStatus)
    public class UpdateDeviceStatusDTO
    {
        public int InventoryId { get; set; }
        public string LifeStatus { get; set; }
    }
}
