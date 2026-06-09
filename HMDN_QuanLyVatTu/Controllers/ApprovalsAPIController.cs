using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using HMS.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/approvals")]
    [CustomApiAuthorize("Approvals")]
    public class ApprovalsAPIController : ApiController
    {
        // GET api/approvals/GetDepartments
        [HttpGet]
        [Route("GetDepartments")]
        public IHttpActionResult GetDepartments()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var depts = db.Departments
                        .Where(x => x.IsActive)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name
                        })
                        .OrderBy(x => x.Name)
                        .ToList();
                    return Ok(depts);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/approvals/GetTickets?userId=X&departmentId=Y
        [HttpGet]
        [Route("GetTickets")]
        public IHttpActionResult GetTickets(int userId = 0, int? departmentId = null)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Lấy toàn bộ phiếu trước
                    var allData = db.Tickets
                        .OrderByDescending(x => x.CreatedAt)
                        .ToList();

                    var users = db.Users.ToList().ToDictionary(u => u.Id);
                    var departments = db.Departments.ToList().ToDictionary(d => d.Id);

                    IEnumerable<Tickets> filteredData;

                    // PHÂN QUYỀN: Admin (Id == 1 hoặc role admin) xem tất cả, các user khác chỉ thấy phiếu do mình gửi đi hoặc gửi tới phòng ban của mình
                    bool isAdmin = false;
                    int? userDeptId = null;
                    if (userId > 0)
                    {
                        var user = db.Users
                            .Include(u => u.UserRoles.Select(ur => ur.Role))
                            .FirstOrDefault(u => u.Id == userId);
                        if (user != null)
                        {
                            userDeptId = user.DepartmentId;
                            var roles = user.UserRoles
                                .Where(ur => ur.Role != null)
                                .Select(ur => ur.Role.Code)
                                .ToList();
                            isAdmin = userId == 1 || roles.Any(r =>
                                string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase)
                            );
                        }
                    }

                    if (isAdmin)
                    {
                        filteredData = allData; // Admin: xem toàn bộ
                    }
                    else if (userId > 1)
                    {
                        // Gmail-like logic: phiếu tôi gửi (CreatedBy == userId) hoặc phiếu gửi tới phòng ban tôi (SendTo == userDeptId)
                        filteredData = allData.Where(t => 
                            t.CreatedBy == userId || 
                            (userDeptId.HasValue && t.SendTo == userDeptId.Value)
                        );
                    }
                    else
                    {
                        // userId = 0 (chưa đăng nhập hoặc không truyền) → không trả về gì
                        filteredData = Enumerable.Empty<Tickets>();
                    }

                    // LỌC THEO PHÒNG BAN NHẬN: Nếu truyền departmentId và > 0, lọc theo phòng ban nhận (t.SendTo)
                    if (departmentId.HasValue && departmentId.Value > 0)
                    {
                        filteredData = filteredData.Where(t => t.SendTo == departmentId.Value).ToList();
                    }

                    var data = filteredData.Select(t =>
                    {
                        users.TryGetValue(t.CreatedBy, out var creator);
                        Department dept = null;
                        if (creator != null && creator.DepartmentId.HasValue)
                        {
                            departments.TryGetValue(creator.DepartmentId.Value, out dept);
                        }

                        Department sendToDept = null;
                        string sendToDeptName = null;
                        if (t.SendTo.HasValue)
                        {
                            if (departments.TryGetValue(t.SendTo.Value, out sendToDept))
                            {
                                sendToDeptName = sendToDept.Name;
                            }
                        }

                        if (string.IsNullOrEmpty(sendToDeptName) && !string.IsNullOrEmpty(t.Note) && t.Note.StartsWith("[Gửi tới:"))
                        {
                            int idxClose = t.Note.IndexOf(']');
                            if (idxClose > 9)
                            {
                                sendToDeptName = t.Note.Substring(9, idxClose - 9).Trim();
                            }
                        }

                        return new ApprovalsListVM
                        {
                            Id = t.Id,
                            TicketCode = t.TicketCode,
                            TicketType = t.TicketType,
                            Status = t.Status,
                            Note = t.Note,
                            CreatedBy = t.CreatedBy,
                            CreatedByName = creator != null ? creator.FullName : null,
                            CreatedByUsername = creator != null ? creator.Username : null,
                            CreatedByPhone = creator != null ? creator.Phone : null,
                            DepartmentName = dept != null ? dept.Name : null,
                            SendToDepartment = sendToDeptName,
                            CreatedAt = t.CreatedAt,
                            CheckedBy = t.CheckedBy,
                            CheckedAt = t.CheckedAt,
                            ApprovedBy = t.ApprovedBy,
                            ApprovedAt = t.ApprovedAt,
                            TransactionDate = t.TransactionDate,
                            Title = t.Title
                        };
                    }).ToList();

                    return Ok(data);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/approvals/GetTicketDetails
        [HttpGet]
        [Route("GetTicketDetails")]
        public IHttpActionResult GetTicketDetails(int ticketId)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var ticket = db.Tickets.FirstOrDefault(t => t.Id == ticketId);
                    if (ticket == null)
                    {
                        return Ok(new List<TicketDetailVM>());
                    }

                    bool hasTicketDetails = db.TicketDetails.Any(x => x.TicketId == ticketId);

                    if (!hasTicketDetails)
                    {
                        var data = db.Inventories
                            .Where(x => x.IdTicket == ticketId)
                            .Select(x => new
                            {
                                x.Id,
                                ItemName = x.Item != null ? x.Item.Name : "N/A",
                                x.SerialNumber,
                                x.Quantity,
                                x.LifeStatus,
                                x.ApprovalStatus,
                                x.ApprovalNote,
                                x.ApprovedQuantity,
                                x.Note
                            })
                            .ToList()
                            .Select(x => new TicketDetailVM
                            {
                                Id = x.Id,
                                ItemName = x.ItemName,
                                SerialNumber = x.SerialNumber,
                                Quantity = x.Quantity,
                                LifeStatus = x.LifeStatus,
                                ApprovalStatus = x.ApprovalStatus,
                                ApprovalNote = x.ApprovalNote,
                                ApprovedQuantity = x.ApprovedQuantity,
                                Note = x.Note
                            })
                            .ToList();
                        return Ok(data);
                    }
                    else
                    {
                        var data = db.TicketDetails
                            .Where(x => x.TicketId == ticketId)
                            .Select(x => new
                            {
                                x.Id,
                                x.ItemName,
                                x.Unit,
                                x.Quantity,
                                x.Note,
                                x.ApprovalStatus,
                                x.ApprovedQuantity,
                                x.ApprovalNote
                            })
                            .ToList()
                            .Select(x => new TicketDetailVM
                            {
                                Id = x.Id,
                                ItemName = x.ItemName,
                                SerialNumber = "",
                                Quantity = x.Quantity,
                                LifeStatus = "active",
                                ApprovalStatus = x.ApprovalStatus,
                                ApprovalNote = x.ApprovalNote,
                                ApprovedQuantity = x.ApprovedQuantity,
                                Note = x.Unit + (string.IsNullOrEmpty(x.Note) ? "" : " | " + x.Note)
                            })
                            .ToList();
                        return Ok(data);
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/approvals/UpdateStatus
        [HttpPost]
        [Route("UpdateStatus")]
        public IHttpActionResult UpdateStatus(UpdateStatusRequest request)
        {
            if (request == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Check if ticket exists
                    var ticket = db.Tickets.FirstOrDefault(t => t.Id == request.TicketId);
                    if (ticket == null)
                    {
                        return Ok(new { success = false, message = "Không tìm thấy yêu cầu." });
                    }

                    // PHÂN QUYỀN: Chỉ cho phép admin, manager hoặc approver phê duyệt/từ chối
                    bool isAuthorized = false;
                    bool canAccessTicket = false;
                    if (request.UserId > 0)
                    {
                        var user = db.Users
                            .Include(u => u.UserRoles.Select(ur => ur.Role))
                            .FirstOrDefault(u => u.Id == request.UserId);
                        if (user != null)
                        {
                            var roles = user.UserRoles
                                .Where(ur => ur.Role != null)
                                .Select(ur => ur.Role.Code)
                                .ToList();
                            isAuthorized = request.UserId == 1 || roles.Any(r =>
                                string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "manager", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "approver", StringComparison.OrdinalIgnoreCase)
                            );

                            if (isAuthorized)
                            {
                                bool isAdmin = request.UserId == 1 || roles.Any(r =>
                                    string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase)
                                );
                                if (isAdmin)
                                {
                                    canAccessTicket = true;
                                }
                                else
                                {
                                    canAccessTicket = ticket.CreatedBy == request.UserId || 
                                                      (user.DepartmentId.HasValue && ticket.SendTo == user.DepartmentId.Value);
                                }
                            }
                        }
                    }

                    if (!isAuthorized)
                    {
                        return Ok(new { success = false, message = "Bạn không có quyền thực hiện hành động này." });
                    }
                    if (!canAccessTicket)
                    {
                        return Ok(new { success = false, message = "Bạn không có quyền phê duyệt/từ chối phiếu thuộc phòng ban khác." });
                    }

                    // [ĐÃ LOẠI BỎ] Logic tự động chèn tin nhắn chat từ ticket.Note vào TicketDiscussions khi duyệt để tránh trùng lặp dữ liệu thảo luận.


                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_Approvals_UpdateStatus @TicketId, @Status, @Note",
                        new SqlParameter("@TicketId", request.TicketId),
                        new SqlParameter("@Status", request.Status),
                        new SqlParameter("@Note", (object)request.Note ?? DBNull.Value)
                    );

                    if (request.Items != null)
                    {
                        // Kiểm tra xem phiếu có dữ liệu trong TicketDetails không
                        bool hasTicketDetails = db.TicketDetails.Any(td => td.TicketId == request.TicketId);

                        if (hasTicketDetails)
                        {
                            foreach (var itemInput in request.Items)
                            {
                                var td = db.TicketDetails.Find(itemInput.Id);
                                if (td != null)
                                {
                                    td.ApprovalStatus = (request.Status == "REJECTED" ? "rejected" : (itemInput.IsApproved ? "approved" : "rejected"));
                                    td.ApprovedQuantity = itemInput.ApprovedQuantity;
                                    td.ApprovalNote = itemInput.ApprovalNote;
                                }
                            }
                        }
                        else
                        {
                            // Fallback: Phiếu cũ lưu trong Inventories
                            foreach (var itemInput in request.Items)
                            {
                                var inv = db.Inventories.Find(itemInput.Id);
                                if (inv != null)
                                {
                                    inv.ApprovalStatus = (request.Status == "REJECTED" ? "rejected" : (itemInput.IsApproved ? "approved" : "rejected"));
                                    inv.ApprovedQuantity = itemInput.ApprovedQuantity;
                                    inv.ApprovalNote = itemInput.ApprovalNote;
                                    inv.ApprovedBy = request.UserId;
                                    inv.ApprovedAt = DateTime.Now;
                                }
                            }
                        }
                        db.SaveChanges();
                    }

                    // Cập nhật người duyệt/người từ chối và thời gian trên Ticket thực tế
                    var ticketToUpdate = db.Tickets.Find(request.TicketId);
                    if (ticketToUpdate != null)
                    {
                        if (request.Status == "APPROVED")
                        {
                            ticketToUpdate.ApprovedBy = request.UserId;
                            ticketToUpdate.ApprovedAt = DateTime.Now;
                        }
                        else if (request.Status == "REJECTED")
                        {
                            ticketToUpdate.CheckedBy = request.UserId;
                            ticketToUpdate.CheckedAt = DateTime.Now;
                        }
                        db.SaveChanges();
                    }

                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // GET api/approvals/GetDiscussions?ticketId=1
        [HttpGet]
        [Route("GetDiscussions")]
        public IHttpActionResult GetDiscussions(int ticketId)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var discussions = db.TicketDiscussions
                        .Where(d => d.TicketId == ticketId)
                        .OrderBy(d => d.CreatedAt)
                        .Select(d => new
                        {
                            d.Id,
                            d.TicketId,
                            d.SenderName,
                            d.Message,
                            d.FilePath,
                            d.FileName,
                            d.FileType,
                            d.IsRevoked,
                            CreatedAt = d.CreatedAt.ToString()
                        })
                        .ToList();

                    return Ok(discussions);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/approvals/SendChatMessage
        [HttpPost]
        [Route("SendChatMessage")]
        public IHttpActionResult SendChatMessage(SendChatMessageRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Tin nhắn không được trống.");
            }

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var ticket = db.Tickets.FirstOrDefault(t => t.Id == request.TicketId);
                    if (ticket == null)
                    {
                        return Ok(new { success = false, message = "Không tìm thấy yêu cầu." });
                    }

                    // Logic bảo mật Server-side: Khóa nếu không phải IMPORT và không phải PENDING
                    if (ticket.TicketType != "IMPORT" && ticket.Status != "PENDING")
                    {
                        return Ok(new { success = false, message = "Mục thảo luận này đã bị đóng." });
                    }

                    // Lưu vào bảng TicketDiscussions
                    var discussion = new TicketDiscussion
                    {
                        TicketId = request.TicketId,
                        SenderName = request.SenderName ?? "Người dùng",
                        Message = request.Message.Trim(),
                        FileType = "TEXT",
                        IsRevoked = false,
                        CreatedAt = DateTime.Now
                    };

                    db.TicketDiscussions.Add(discussion);
                    db.SaveChanges();

                    return Ok(new
                    {
                        success = true,
                        item = new
                        {
                            discussion.Id,
                            discussion.TicketId,
                            discussion.SenderName,
                            discussion.Message,
                            discussion.FilePath,
                            discussion.FileName,
                            discussion.FileType,
                            discussion.IsRevoked,
                            CreatedAt = discussion.CreatedAt.ToString()
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // POST api/approvals/UploadChatFile
        [HttpPost]
        [Route("UploadChatFile")]
        public IHttpActionResult UploadChatFile()
        {
            var httpRequest = HttpContext.Current.Request;
            if (httpRequest.Files.Count == 0)
            {
                return BadRequest("Không tìm thấy file upload.");
            }

            // Đọc ticketId và senderName từ form fields
            int ticketId;
            if (!int.TryParse(httpRequest.Form["ticketId"], out ticketId))
            {
                return BadRequest("Thiếu ticketId.");
            }
            string senderName = httpRequest.Form["senderName"] ?? "Người dùng";

            try
            {
                var postedFile = httpRequest.Files[0];
                if (postedFile == null || postedFile.ContentLength == 0)
                {
                    return BadRequest("File không hợp lệ.");
                }

                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var docExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                var videoExtensions = new[] { ".mp4", ".mov", ".avi" };
                var allowedExtensions = imageExtensions.Concat(docExtensions).Concat(videoExtensions).ToArray();

                string originalFileName = Path.GetFileName(postedFile.FileName);
                string fileExtension = Path.GetExtension(originalFileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Định dạng file không được phép.");
                }

                // Lưu file vật lý vào ~/Uploads
                string uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                string serverPath = HttpContext.Current.Server.MapPath("~/Uploads");
                if (!Directory.Exists(serverPath))
                {
                    Directory.CreateDirectory(serverPath);
                }
                string physicalPath = Path.Combine(serverPath, uniqueFileName);
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

                // Lưu bản ghi vào bảng TicketDiscussions
                using (var db = new HospitalAssetDbContext())
                {
                    var ticketExists = db.Tickets.Any(t => t.Id == ticketId);
                    if (!ticketExists)
                    {
                        return Ok(new { success = false, message = "Không tìm thấy yêu cầu." });
                    }

                    var discussion = new TicketDiscussion
                    {
                        TicketId = ticketId,
                        SenderName = senderName,
                        Message = null,
                        FilePath = relativePath,
                        FileName = originalFileName,
                        FileType = fileType,
                        IsRevoked = false,
                        CreatedAt = DateTime.Now
                    };

                    db.TicketDiscussions.Add(discussion);
                    db.SaveChanges();

                    return Ok(new
                    {
                        success = true,
                        item = new
                        {
                            discussion.Id,
                            discussion.TicketId,
                            discussion.SenderName,
                            discussion.Message,
                            discussion.FilePath,
                            discussion.FileName,
                            discussion.FileType,
                            discussion.IsRevoked,
                            CreatedAt = discussion.CreatedAt.ToString()
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/approvals/RevokeChatMessage
        [HttpPost]
        [Route("RevokeChatMessage")]
        public IHttpActionResult RevokeChatMessage(RevokeMessageRequest request)
        {
            if (request == null || request.MessageId <= 0)
            {
                return BadRequest("Yêu cầu không hợp lệ.");
            }

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var discussion = db.TicketDiscussions.Find(request.MessageId);
                    if (discussion == null)
                    {
                        return Ok(new { success = false, message = "Không tìm thấy tin nhắn." });
                    }

                    discussion.IsRevoked = true;
                    db.SaveChanges();

                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // POST api/approvals/ClearTicketDiscussions
        [HttpPost]
        [Route("ClearTicketDiscussions")]
        public IHttpActionResult ClearTicketDiscussions()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    db.Database.ExecuteSqlCommand("EXEC sp_ClearTicketDiscussions");
                    return Ok(new { success = true, message = "Đã xóa sạch dữ liệu mẫu thảo luận và reset Identity thành công!" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Lỗi khi thực thi stored procedure: " + ex.Message });
            }
        }
    }

    public class SendChatMessageRequest
    {
        public int TicketId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
    }

    public class RevokeMessageRequest
    {
        public int MessageId { get; set; }
    }


    public class ApprovalsListVM
    {
        public int Id { get; set; }
        public string TicketCode { get; set; }
        public string TicketType { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public string CreatedByUsername { get; set; }
        public string CreatedByPhone { get; set; }
        public string DepartmentName { get; set; }
        public string SendToDepartment { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CheckedBy { get; set; }
        public DateTime? CheckedAt { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string Title { get; set; }
    }

    public class TicketDetailVM
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public string SerialNumber { get; set; }
        public int Quantity { get; set; }
        public string LifeStatus { get; set; }
        public string ApprovalStatus { get; set; }
        public string ApprovalNote { get; set; }
        public int? ApprovedQuantity { get; set; }
        public string Note { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int TicketId { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public List<TicketItemApprovalInput> Items { get; set; }
        public int UserId { get; set; }
    }

    public class TicketItemApprovalInput
    {
        public int Id { get; set; }
        public int ApprovedQuantity { get; set; }
        public bool IsApproved { get; set; }
        public string ApprovalNote { get; set; }
    }
}