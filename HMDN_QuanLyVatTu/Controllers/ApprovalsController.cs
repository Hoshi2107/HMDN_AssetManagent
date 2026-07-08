using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HMS.Data;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Approvals")]
    public class ApprovalsController : Controller
    {
        private HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET: Approvals
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Approvals";
            return View();
        }

        [HttpGet]
        public JsonResult GetTickets()
        {
            try
            {
                var userIdSession = Session["UserId"] as int?;
                int userId = userIdSession ?? 0;

                var users = db.Users.ToList().ToDictionary(u => u.Id);
                var departments = db.Departments.ToList().ToDictionary(d => d.Id);

                // PHÂN QUYỀN DẠNG GMAIL
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

                var query = db.Tickets.AsQueryable();
                if (!isAdmin)
                {
                    if (userId > 1)
                    {
                        query = query.Where(t => 
                            t.CreatedBy == userId || 
                            (userDeptId.HasValue && t.SendTo == userDeptId.Value)
                        );
                    }
                    else
                    {
                        query = query.Where(t => false); // Chưa đăng nhập hoặc khách
                    }
                }

                var data = query
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList()
                    .Select(t =>
                    {
                        users.TryGetValue(t.CreatedBy, out var creator);
                        HMS.Models.Department dept = null;
                        if (creator != null && creator.DepartmentId.HasValue)
                        {
                            departments.TryGetValue(creator.DepartmentId.Value, out dept);
                        }

                        HMS.Models.Department sendToDept = null;
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

                        return new
                        {
                            t.Id,
                            t.TicketCode,
                            t.TicketType,
                            t.Status,
                            t.Note,
                            t.CreatedBy,
                            CreatedByName = creator != null ? creator.FullName : null,
                            CreatedByUsername = creator != null ? creator.Username : null,
                            CreatedByPhone = creator != null ? creator.Phone : null,
                            DepartmentName = dept != null ? dept.Name : null,
                            SendToDepartment = sendToDeptName,
                            CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
                            t.CheckedBy,
                            CheckedAt = t.CheckedAt.HasValue ? t.CheckedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                            t.ApprovedBy,
                            ApprovedAt = t.ApprovedAt.HasValue ? t.ApprovedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                            TransactionDate = t.TransactionDate.HasValue ? t.TransactionDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                            Title = t.Title
                        };
                    });

                return Json(data, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetTicketDetails(int ticketId)
        {
            var ticket = db.Tickets.FirstOrDefault(t => t.Id == ticketId);
            if (ticket == null)
            {
                return Json(new object[] { }, JsonRequestBehavior.AllowGet);
            }

            bool hasTicketDetails = db.TicketDetails.Any(x => x.TicketId == ticketId);

            if (!hasTicketDetails)
            {
                var details = db.Inventories
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
                        Note = x.Note,
                        x.AssetCode
                    })
                    .ToList();
                return Json(details, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var ticketDetailsList = db.TicketDetails
                    .Where(x => x.TicketId == ticketId)
                    .ToList();

                var assetCodes = new List<string>();
                foreach (var td in ticketDetailsList)
                {
                    string assetCode = td.ItemName;
                    if (td.ItemName != null && td.ItemName.Contains(" (SN: "))
                    {
                        int idx = td.ItemName.IndexOf(" (SN: ");
                        assetCode = td.ItemName.Substring(0, idx).Trim();
                    }
                    if (!string.IsNullOrEmpty(assetCode))
                    {
                        assetCodes.Add(assetCode);
                    }
                }

                var inventories = db.Inventories
                    .Include(i => i.Item)
                    .Where(i => assetCodes.Contains(i.AssetCode))
                    .ToList()
                    .GroupBy(i => i.AssetCode)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault());

                var details = ticketDetailsList.Select(x =>
                {
                    string assetCode = x.ItemName;
                    string serialNumber = "";
                    if (x.ItemName != null && x.ItemName.Contains(" (SN: "))
                    {
                        int idx = x.ItemName.IndexOf(" (SN: ");
                        assetCode = x.ItemName.Substring(0, idx).Trim();
                        serialNumber = x.ItemName.Substring(idx + 6).Replace(")", "").Trim();
                    }

                    string displayName = x.ItemName;
                    string finalSerial = serialNumber;
                    string finalAssetCode = "";

                    if (ticket.TicketType == "SUPPORT" || ticket.TicketType == "REPAIR")
                    {
                        finalAssetCode = assetCode;
                        if (!string.IsNullOrEmpty(assetCode) && inventories.TryGetValue(assetCode, out var inv))
                        {
                            displayName = inv.Item != null ? inv.Item.Name : "N/A";
                            if (string.IsNullOrEmpty(finalSerial))
                            {
                                finalSerial = inv.SerialNumber ?? "";
                            }
                        }
                        else
                        {
                            displayName = assetCode;
                        }
                    }

                    return new
                    {
                        x.Id,
                        ItemName = displayName,
                        SerialNumber = finalSerial,
                        x.Quantity,
                        LifeStatus = "active",
                        x.ApprovalStatus,
                        x.ApprovalNote,
                        x.ApprovedQuantity,
                        Note = x.Unit + (string.IsNullOrEmpty(x.Note) ? "" : " | " + x.Note),
                        AssetCode = finalAssetCode
                    };
                }).ToList();

                return Json(details, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult UpdateStatus(int ticketId, string status, string note)
        {
            var ticket = db.Tickets.Find(ticketId);
            if (ticket == null)
            {
                return Json(new { success = false, message = "Không tìm thấy yêu cầu." });
            }

            ticket.Status = status;

            if (status == "APPROVED")
            {
                ticket.ApprovedBy = 1;
                ticket.ApprovedAt = System.DateTime.Now;
            }
            else if (status == "REJECTED")
            {
                ticket.CheckedBy = 1;
                ticket.CheckedAt = System.DateTime.Now;
            }

            db.SaveChanges();
            return Json(new { success = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}