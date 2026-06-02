using System;
using System.Linq;
using System.Web.Mvc;
using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using HMS.Models.Catalog;
using HMS.Models.Inventory;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("CreateTicket")]
    public class CreateTicketController : Controller
    {
        private HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET: CreateTicket
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "CreateTicket";
            return View("CreateTicket");
        }

        [HttpGet]
        public JsonResult GetUserInfo(int userId)
        {
            try
            {
                var user = db.Users.Include("Department").FirstOrDefault(u => u.Id == userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    fullName = user.FullName,
                    departmentName = user.Department != null ? user.Department.Name : "Chưa có phòng ban"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetAllItems()
        {
            try
            {
                var items = db.Items
                    .Where(x => x.IsActive)
                    .Select(x => new
                    {
                        x.Id,
                        x.Code,
                        x.Name,
                        x.Unit
                    })
                    .ToList()
                    .OrderBy(x => x.Name);
                return Json(items, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetActiveDepartments()
        {
            try
            {
                var depts = db.Departments
                    .Where(x => x.IsActive)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name
                    })
                    .ToList()
                    .OrderBy(x => x.Name);
                return Json(depts, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetAllInventoryAssets()
        {
            try
            {
                var assets = db.Inventories
                    .Where(x => !string.IsNullOrEmpty(x.AssetCode))
                    .Select(x => new
                    {
                        AssetCode = x.AssetCode,
                        SerialNumber = x.SerialNumber
                    })
                    .GroupBy(x => x.AssetCode)
                    .Select(g => g.FirstOrDefault())
                    .OrderBy(x => x.AssetCode)
                    .ToList();
                return Json(assets, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public class TicketItemDto
        {
            public string ItemName { get; set; }
            public string SerialNumber { get; set; }
            public int Quantity { get; set; }
            public string Note { get; set; }
        }

        public class CreateTicketPayload
        {
            public string TicketType { get; set; }
            public string AssetType { get; set; }
            public string Note { get; set; }
            public int UserId { get; set; }
            public string SenderName { get; set; }  // Tên người yêu cầu (dùng cho TicketDiscussions)
            public string ReasonDetails { get; set; }
            public int? TargetDepartmentId { get; set; }
            public System.Collections.Generic.List<TicketItemDto> Devices { get; set; }
        }

        [HttpPost]
        public JsonResult CreateNewTicket()
        {
            try
            {
                var request = HttpContext.Request;
                string payloadStr = request.Form["payload"];
                if (string.IsNullOrEmpty(payloadStr))
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                // Deserialize payload JSON using Newtonsoft.Json
                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<CreateTicketPayload>(payloadStr);
                if (payload == null)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                // --- Backend Validation Sync ---
                if (string.IsNullOrWhiteSpace(payload.ReasonDetails))
                {
                    return Json(new { success = false, message = "Vui lòng nhập lý do chi tiết yêu cầu!" });
                }

                // Get user's department
                int? userDeptId = null;
                var user = db.Users.Find(payload.UserId);
                if (user != null)
                {
                    userDeptId = user.DepartmentId;
                }

                // Lấy thông tin phòng ban nhận (Target Department) nếu có
                string targetDeptName = "";
                if (payload.TargetDepartmentId.HasValue)
                {
                    var targetDept = db.Departments.Find(payload.TargetDepartmentId.Value);
                    if (targetDept != null)
                    {
                        targetDeptName = targetDept.Name;
                    }
                }

                // Ghép phòng ban nhận vào Note để người duyệt dễ theo dõi
                string originalNote = !string.IsNullOrWhiteSpace(payload.Note) 
                    ? payload.Note.Trim() 
                    : (payload.ReasonDetails != null ? payload.ReasonDetails.Trim() : "");
                
                string finalNote = !string.IsNullOrEmpty(targetDeptName)
                    ? $"[Gửi tới: {targetDeptName}] {originalNote}"
                    : originalNote;

                // 1. Save ticket (Fallback to ReasonDetails if Note is empty/whitespace)
                var ticket = new Tickets
                {
                    TicketCode = GenerateTicketCode(payload.TicketType),
                    TicketType = payload.TicketType,
                    Status = "PENDING",
                    Note = finalNote,
                    CreatedBy = payload.UserId,
                    CreatedAt = DateTime.Now,
                    SendTo = payload.TargetDepartmentId
                };

                db.Tickets.Add(ticket);
                db.SaveChanges(); // Generates ticket.Id

                // 1.5. Save reason details as a text message in TicketDiscussions if present
                // Xác định tên người gửi: ưu tiên payload.SenderName, fallback sang user.FullName
                string resolvedSenderName = !string.IsNullOrWhiteSpace(payload.SenderName)
                    ? payload.SenderName.Trim()
                    : (user != null ? user.FullName : "Người yêu cầu");

                if (!string.IsNullOrWhiteSpace(payload.ReasonDetails))
                {
                    var reasonMsg = new TicketDiscussion
                    {
                        TicketId = ticket.Id,
                        SenderName = resolvedSenderName,
                        Message = payload.ReasonDetails.Trim(),
                        FileType = "TEXT",
                        IsRevoked = false,
                        CreatedAt = ticket.CreatedAt
                    };
                    db.TicketDiscussions.Add(reasonMsg);
                    db.SaveChanges();
                }

                // 2. Save items (Inventories)
                if (payload.Devices != null && payload.Devices.Count > 0)
                {
                    foreach (var device in payload.Devices)
                    {
                        if (string.IsNullOrWhiteSpace(device.ItemName)) continue;

                        // Check if an item with the same name exists
                        string trimmedName = device.ItemName.Trim();
                        var item = db.Items.FirstOrDefault(x => x.Name.Trim().ToLower() == trimmedName.ToLower());
                        if (item == null)
                        {
                            // Get first group or create a default group
                            var group = db.Groups.FirstOrDefault();
                            int groupId = 1;
                            if (group != null)
                            {
                                groupId = group.Id;
                            }
                            else
                            {
                                var newGroup = new Group
                                {
                                    Code = "DEFAULT",
                                    Name = "Nhóm mặc định",
                                    IsActive = true,
                                    CreatedAt = DateTime.Now
                                };
                                db.Groups.Add(newGroup);
                                db.SaveChanges();
                                groupId = newGroup.Id;
                            }

                            // Create new item under catalog
                            item = new Item
                            {
                                GroupId = groupId,
                                Code = "ITEM_" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                                Name = trimmedName,
                                Unit = "Cái",
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            db.Items.Add(item);
                            db.SaveChanges(); // Generates item.Id
                        }

                        var inventory = new HMS.Models.Inventory.Inventory
                        {
                            ItemId = item.Id,
                            AssetCode = "TS-" + DateTime.Now.ToString("yyMMdd") + Guid.NewGuid().ToString().Substring(0, 4).ToUpper(),
                            SerialNumber = string.IsNullOrWhiteSpace(device.SerialNumber)
                                ? "SN-" + DateTime.Now.ToString("yyMMdd") + Guid.NewGuid().ToString().Substring(0, 4).ToUpper()
                                : device.SerialNumber.Trim(),
                            Quantity = device.Quantity,
                            IdTicket = ticket.Id,
                            ApprovalStatus = "PENDING",
                            LifeStatus = "active",
                            ImportDate = DateTime.Now,
                            UnitPrice = 0,
                            TotalPrice = 0,
                            CreatedBy = payload.UserId,
                            CreatedAt = DateTime.Now,
                            Note = device.Note,
                            DepartmentId = payload.TargetDepartmentId ?? userDeptId,
                            QrCode = "QR-" + Guid.NewGuid().ToString("N").ToUpper()
                        };
                        db.Inventories.Add(inventory);
                    }
                    db.SaveChanges();
                }

                // 3. Save uploaded files as TicketDiscussions
                if (request.Files.Count > 0)
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var videoExtensions = new[] { ".mp4", ".mov", ".avi" };

                    string serverPath = Server.MapPath("~/Uploads");
                    if (!System.IO.Directory.Exists(serverPath))
                    {
                        System.IO.Directory.CreateDirectory(serverPath);
                    }

                    for (int i = 0; i < request.Files.Count; i++)
                    {
                        var postedFile = request.Files[i];
                        if (postedFile == null || postedFile.ContentLength == 0) continue;

                        string originalFileName = System.IO.Path.GetFileName(postedFile.FileName);
                        string fileExtension = System.IO.Path.GetExtension(originalFileName).ToLower();

                        string uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                        string physicalPath = System.IO.Path.Combine(serverPath, uniqueFileName);
                        postedFile.SaveAs(physicalPath);

                        string relativePath = "/Uploads/" + uniqueFileName;
                        string fileType = "FILE";
                        if (imageExtensions.Contains(fileExtension))
                        {
                            fileType = "IMAGE";
                        }
                        else if (videoExtensions.Contains(fileExtension))
                        {
                            fileType = "VIDEO";
                        }

                        var discussion = new TicketDiscussion
                        {
                            TicketId = ticket.Id,
                            SenderName = resolvedSenderName,  // Dùng tên thực của người yêu cầu
                            Message = null,
                            FilePath = relativePath,
                            FileName = originalFileName,
                            FileType = fileType,
                            IsRevoked = false,
                            CreatedAt = DateTime.Now
                        };
                        db.TicketDiscussions.Add(discussion);
                    }
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Tạo phiếu thành công!", ticketId = ticket.Id, ticketCode = ticket.TicketCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private string GenerateTicketCode(string ticketType)
        {
            string prefix = "PX";
            if (ticketType == "IMPORT") prefix = "PN";
            else if (ticketType == "TRANSFER") prefix = "DC";
            else if (ticketType == "HoTro") prefix = "HT";

            int count = db.Tickets.Count(t => t.TicketType == ticketType);
            return prefix + (count + 1).ToString("D4");
        }
    }
}

