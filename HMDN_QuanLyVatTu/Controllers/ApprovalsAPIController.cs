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
    public class ApprovalsAPIController : ApiController
    {
        // GET api/approvals/GetTickets?userId=X
        [HttpGet]
        [Route("GetTickets")]
        public IHttpActionResult GetTickets(int userId = 0)
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

                    // PHÂN QUYỀN: Admin (Id == 1 hoặc role admin/manager/approver) xem tất cả, user khác chỉ xem phiếu của mình
                    bool canApproveAll = false;
                    if (userId > 0)
                    {
                        var user = db.Users
                            .Include(u => u.UserRoles.Select(ur => ur.Role))
                            .FirstOrDefault(u => u.Id == userId);
                        if (user != null)
                        {
                            var roles = user.UserRoles
                                .Where(ur => ur.Role != null)
                                .Select(ur => ur.Role.Code)
                                .ToList();
                            canApproveAll = userId == 1 || roles.Any(r =>
                                string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "manager", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r, "approver", StringComparison.OrdinalIgnoreCase)
                            );
                        }
                    }

                    if (canApproveAll)
                    {
                        filteredData = allData; // Admin/Manager/Approver: xem toàn bộ
                    }
                    else if (userId > 1)
                    {
                        filteredData = allData.Where(t => t.CreatedBy == userId); // User/KTV: chỉ phiếu của mình
                    }
                    else
                    {
                        // userId = 0 (chưa đăng nhập hoặc không truyền) → không trả về gì
                        filteredData = Enumerable.Empty<Tickets>();
                    }

                    var data = filteredData.Select(t =>
                    {
                        users.TryGetValue(t.CreatedBy, out var creator);
                        Department dept = null;
                        if (creator != null && creator.DepartmentId.HasValue)
                        {
                            departments.TryGetValue(creator.DepartmentId.Value, out dept);
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
                            DepartmentName = dept != null ? dept.Name : null,
                            CreatedAt = t.CreatedAt,
                            CheckedBy = t.CheckedBy,
                            CheckedAt = t.CheckedAt,
                            ApprovedBy = t.ApprovedBy,
                            ApprovedAt = t.ApprovedAt,
                            TransactionDate = t.TransactionDate
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
                    // Dùng EF trực tiếp, project sang anonymous type trước để tránh lỗi EF projection
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
                            x.ApprovedQuantity
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
                            ApprovedQuantity = x.ApprovedQuantity
                        })
                        .ToList();
                    return Ok(data);
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
                        }
                    }

                    if (!isAuthorized)
                    {
                        return Ok(new { success = false, message = "Bạn không có quyền thực hiện hành động này." });
                    }

                    // Preserve the original ticket Note (ReasonDetails) in TicketDiscussions before it gets overwritten by the approver's note
                    if (!string.IsNullOrWhiteSpace(ticket.Note))
                    {
                        string trimmedNote = ticket.Note.Trim();
                        bool alreadySaved = db.TicketDiscussions.Any(d => d.TicketId == ticket.Id && d.Message == trimmedNote);
                        if (!alreadySaved)
                        {
                            var creator = db.Users.Find(ticket.CreatedBy);
                            var reasonMsg = new TicketDiscussion
                            {
                                TicketId = ticket.Id,
                                SenderName = creator != null ? creator.FullName : "Người yêu cầu",
                                Message = trimmedNote,
                                FileType = "TEXT",
                                IsRevoked = false,
                                CreatedAt = ticket.CreatedAt
                            };
                            db.TicketDiscussions.Add(reasonMsg);
                            db.SaveChanges();
                        }
                    }

                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_Approvals_UpdateStatus @TicketId, @Status, @Note",
                        new SqlParameter("@TicketId", request.TicketId),
                        new SqlParameter("@Status", request.Status),
                        new SqlParameter("@Note", (object)request.Note ?? DBNull.Value)
                    );

                    if (request.Items != null)
                    {
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
        public string DepartmentName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CheckedBy { get; set; }
        public DateTime? CheckedAt { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? TransactionDate { get; set; }
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