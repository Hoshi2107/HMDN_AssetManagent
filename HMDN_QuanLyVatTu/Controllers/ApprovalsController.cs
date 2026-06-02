using System;
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
                var users = db.Users.ToList().ToDictionary(u => u.Id);
                var departments = db.Departments.ToList().ToDictionary(d => d.Id);

                var data = db.Tickets
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
                            DepartmentName = dept != null ? dept.Name : null,
                            SendToDepartment = sendToDeptName,
                            CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
                            t.CheckedBy,
                            CheckedAt = t.CheckedAt.HasValue ? t.CheckedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                            t.ApprovedBy,
                            ApprovedAt = t.ApprovedAt.HasValue ? t.ApprovedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                            TransactionDate = t.TransactionDate.HasValue ? t.TransactionDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
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
                    x.ApprovedQuantity
                })
                .ToList();
            return Json(details, JsonRequestBehavior.AllowGet);
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