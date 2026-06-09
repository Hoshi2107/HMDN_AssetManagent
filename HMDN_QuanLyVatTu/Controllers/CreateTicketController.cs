using System;
using System.Collections.Generic;
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
        private static readonly object _ticketCodeLock = new object();

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
                        AssetName = x.Item != null ? x.Item.Name : "",
                        Model = x.Item != null ? x.Item.Model : "",
                        SerialNumber = x.SerialNumber,
                        DepartmentName = x.Department != null ? x.Department.Name : ""
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
            public string Unit { get; set; }
            public string Note { get; set; }
        }

        public class CreateTicketPayload
        {
            public string Title { get; set; }
            public string TicketType { get; set; }
            public string AssetType { get; set; }
            public string Note { get; set; }
            public int UserId { get; set; }
            public string SenderName { get; set; }  // Tên người yêu cầu (dùng cho TicketDiscussions)
            public string ReasonDetails { get; set; }
            public string Proposal { get; set; }
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
                if (payload.TicketType == "SUPPORT" || payload.TicketType == "REPAIR")
                {
                    if (string.IsNullOrWhiteSpace(payload.ReasonDetails))
                    {
                        return Json(new { success = false, message = "Vui lòng nhập lý do chi tiết yêu cầu!" });
                    }
                    if (string.IsNullOrWhiteSpace(payload.Proposal))
                    {
                        return Json(new { success = false, message = "Vui lòng nhập đề nghị!" });
                    }
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
                Tickets ticket;
                lock (_ticketCodeLock)
                {
                    ticket = new Tickets
                    {
                        TicketCode = GenerateTicketCode(payload.TicketType),
                        TicketType = payload.TicketType,
                        Status = "PENDING",
                        Note = finalNote,
                        CreatedBy = payload.UserId,
                        CreatedAt = DateTime.Now,
                        SendTo = payload.TargetDepartmentId,
                        Title = payload.Title
                    };

                    db.Tickets.Add(ticket);
                    db.SaveChanges(); // Generates ticket.Id
                }

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
                }

                if (!string.IsNullOrWhiteSpace(payload.Proposal))
                {
                    var proposalMsg = new TicketDiscussion
                    {
                        TicketId = ticket.Id,
                        SenderName = resolvedSenderName,
                        Message = "[Đề nghị] " + payload.Proposal.Trim(),
                        FileType = "TEXT",
                        IsRevoked = false,
                        CreatedAt = ticket.CreatedAt.AddSeconds(1)
                    };
                    db.TicketDiscussions.Add(proposalMsg);
                }
                db.SaveChanges();

                // 2. Save items (Inventories / TicketDetails)
                if (payload.Devices != null && payload.Devices.Count > 0)
                {
                    bool isDeviceTicket = payload.TicketType == "SUPPORT" || payload.TicketType == "REPAIR";

                    if (isDeviceTicket)
                    {
                        foreach (var device in payload.Devices)
                        {
                            if (string.IsNullOrWhiteSpace(device.ItemName)) continue;

                            string assetCode = device.ItemName.Trim();
                            var existingInventory = db.Inventories.FirstOrDefault(i => i.AssetCode == assetCode);

                            if (existingInventory != null)
                            {
                                // Tạo phiếu sửa chữa bên bảo trì
                                var mLog = new MaintenanceLog
                                {
                                    InventoryId = existingInventory.Id,
                                    MaintenanceType = "corrective",
                                    Title = "Sửa chữa từ phiếu " + ticket.TicketCode,
                                    Description = string.IsNullOrWhiteSpace(payload.ReasonDetails) ? "Yêu cầu sửa chữa" : payload.ReasonDetails,
                                    ErrorDescription = payload.ReasonDetails,
                                    StartDate = DateTime.Now,
                                    Status = "open",
                                    Priority = "normal",
                                    ReportedBy = payload.UserId,
                                    CreatedAt = DateTime.Now,
                                    TicketId = ticket.Id
                                };
                                db.MaintenanceLogs.Add(mLog);

                                // Cập nhật trạng thái thiết bị thành hỏng
                                existingInventory.LifeStatus = "suspended";
                            }
                            
                            // Tạo chi tiết yêu cầu để hiện bên phê duyệt (luôn tạo kể cả khi existingInventory == null)
                            var detail = new TicketDetail
                            {
                                TicketId = ticket.Id,
                                ItemName = assetCode + (string.IsNullOrWhiteSpace(device.SerialNumber) ? "" : $" (SN: {device.SerialNumber})"),
                                Unit = string.IsNullOrWhiteSpace(device.Unit) ? "Cái" : device.Unit.Trim(),
                                Quantity = device.Quantity,
                                Note = device.Note,
                                ApprovalStatus = "PENDING",
                                ApprovedQuantity = device.Quantity,
                                ApprovalNote = ""
                            };
                            db.TicketDetails.Add(detail);
                        }
                    }
                    else
                    {
                        foreach (var device in payload.Devices)
                        {
                            if (string.IsNullOrWhiteSpace(device.ItemName)) continue;

                            var detail = new TicketDetail
                            {
                                TicketId = ticket.Id,
                                ItemName = device.ItemName.Trim(),
                                Unit = string.IsNullOrWhiteSpace(device.Unit) ? "Cái" : device.Unit.Trim(),
                                Quantity = device.Quantity,
                                Note = device.Note,
                                ApprovalStatus = "PENDING",
                                ApprovedQuantity = device.Quantity,
                                ApprovalNote = ""
                            };
                            db.TicketDetails.Add(detail);
                        }
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
            // Map loại phiếu sang prefix mã
            string prefix;
            switch (ticketType)
            {
                case "IMPORT":
                    prefix = "PN";
                    break;
                case "TRANSFER":
                    prefix = "DC";
                    break;
                case "SUPPORT":
                case "HoTro":
                    prefix = "HT";
                    break;
                case "REPAIR":
                    prefix = "SC";
                    break;
                default: // EXPORT và các loại khác
                    prefix = "PX";
                    break;
            }

            // Lấy tất cả TicketCode bắt đầu bằng prefix hiện tại
            List<string> existingCodes = db.Tickets
                .Where(t => t.TicketCode.StartsWith(prefix))
                .Select(t => t.TicketCode)
                .ToList();

            // Parse phần hậu tố số để tìm giá trị MAX
            int maxNumber = 0;
            foreach (var code in existingCodes)
            {
                if (string.IsNullOrEmpty(code) || code.Length <= prefix.Length)
                    continue;

                string suffix = code.Substring(prefix.Length);
                int num;
                if (int.TryParse(suffix, out num) && num > maxNumber)
                {
                    maxNumber = num;
                }
            }

            // Tăng lên 1 và đảm bảo duy nhất bằng vòng lặp do-while (D5 định dạng)
            string newCode;
            do
            {
                maxNumber++;
                newCode = prefix + maxNumber.ToString("D5");
            }
            while (db.Tickets.Any(t => t.TicketCode == newCode));

            return newCode;
        }
    }
}

